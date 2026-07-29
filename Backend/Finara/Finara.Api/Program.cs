using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Finara.Api.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Finara.Api.Endpoints;
using Finara.Api.HealthChecks;
using Finara.Api.ExceptionHandling;
using Finara.Api.Middleware;
using Finara.Api.RateLimiting;
using Finara.Api.Realtime;
using Finara.Api.Security;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;
using Finara.Infrastructure.Automation;
using Finara.Infrastructure.MockData;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Varsayılan ILogger (sadece konsol, yapılandırılmamış) Serilog ile değiştiriliyor —
// yapılandırılmış (structured) alanlar, dosyaya kalıcı yazma ve appsettings'teki
// "Serilog" bölümünden log seviyesi kontrolü sağlıyor. CorrelationIdMiddleware'in
// logger.BeginScope ile eklediği "CorrelationId" alanı, Serilog'un Microsoft.Extensions.Logging
// köprüsü tarafından otomatik olarak bir log özelliğine (property) dönüştürülüyor —
// ekstra bir bağlama kodu gerekmedi.
// {CorrelationId} — CorrelationIdMiddleware'in BeginScope ile eklediği alan; istek
// kapsamı dışındaki loglarda (ör. arka plan servisleri) bu alan yok, Serilog eksik bir
// property'i sessizce boş render eder (hata vermez, şablon bozulmaz).
const string logOutputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}";
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: logOutputTemplate)
    .WriteTo.File("logs/finara-.log", outputTemplate: logOutputTemplate, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// Garanti/Ollama gibi gerçek dış HTTP bağımlılıklarının GEÇİCİ (transient) hatalarında
// (5xx, 408, ağ kopması) 3 kez üstel geri çekilmeyle yeniden denenir; devrede kalan bir
// hata art arda 5 kez tekrarlanırsa devre kesici (circuit breaker) 30 saniye boyunca
// isteği hiç denemeden hızlıca başarısız sayar — çökmüş bir dış servise karşı sürekli
// bekleyerek kaynak tüketmemek için. SMTP/Twilio bu HttpClient tabanlı politikayı
// KULLANAMIYOR (kendi SDK'ları IHttpClientFactory üzerinden çalışmıyor) — onlar için
// aynı zamanlamayı tekrar eden düz Polly policy'leri Finara.Infrastructure/Resilience'ta.
static IAsyncPolicy<HttpResponseMessage> BuildHttpRetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

static IAsyncPolicy<HttpResponseMessage> BuildHttpCircuitBreakerPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<FinaraDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Data Protection anahtar halkasının deposu — bkz. DataProtectionKeysDbContext'teki not
// (neden ayrı bir DbContext, neden bu proje içinde tanımlı).
builder.Services.AddDbContext<DataProtectionKeysDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Liveness (/health/live): sürecin ayakta olup olmadığını kontrol eder, hiçbir dış
// bağımlılığa bakmaz. Readiness (/health/ready): trafiğe hazır olup olmadığını,
// yani DB bağlantısının kurulabildiğini kontrol eder — "ready" tag'iyle işaretlenen
// kontroller ikinci uçta, hiçbiri birincisinde çalışır (orkestrasyon araçlarının
// standart liveness/readiness ayrımı).
// Otomasyonların (segmentasyon/teklif üretimi, haftalık özet) son başarılı çalışma zamanını
// tutan tekil kayıt — daha önce bir tur sessizce çökerse (ör. hata) sadece log'a yazılıyordu,
// kimse fark etmiyordu. AutomationHealthCheck bunu /health/automation ucundan dışa veriyor.
builder.Services.AddSingleton<IAutomationHealthTracker, AutomationHealthTracker>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinaraDbContext>("database", tags: ["ready"])
    .AddCheck<AutomationHealthCheck>("automation", tags: ["automation"]);

// Anahtar halkası artık dosya sistemi yerine veritabanında (zaten tüm instance'lar arasında
// paylaşılan tek kaynak) tutuluyor — bkz. DataProtectionKeysDbContext'teki not (önceki
// oturumdaki CS1061 derleme sorununun kökü ve çözümü orada belgelendi).
builder.Services.AddDataProtection()
    .SetApplicationName("Finara")
    .PersistKeysToDbContext<DataProtectionKeysDbContext>();

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IReportService, ReportService>();

// Faz 8.1 — multi-tenancy. HttpContextAccessor'ı gerektiren tek yer burası; CurrentUserContext
// Api katmanında yaşıyor (Infrastructure "plain" SDK projesi, ASP.NET Core paylaşılan
// framework'üne bağımlı olmamalı) ama arayüzü (ICurrentUserContext) Application'da.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

builder.Services.Configure<PayloadGuardOptions>(builder.Configuration.GetSection("PayloadGuard"));

// Zamanlanmış otomasyon — segmentasyonu ve teklif üretimini periyodik olarak,
// manuel butonlara ihtiyaç kalmadan tetikler (bkz. FinaraAutomationService).
builder.Services.Configure<AutomationOptions>(builder.Configuration.GetSection("Automation"));
builder.Services.AddHostedService<FinaraAutomationService>();

// Her BankAdmin'e kendi bankası için haftalık özet e-postası (bkz. WeeklyDigestService).
builder.Services.Configure<WeeklyDigestOptions>(builder.Configuration.GetSection("WeeklyDigest"));
builder.Services.AddHostedService<WeeklyDigestService>();

// Faz 8.2 — Bogus tabanlı mock üretici, BankDataProviderType.Mock anahtarıyla keyed
// service olarak kayıtlı. IBankDataProviderFactory'yi tüketen kod değişmeden yeni bir
// enum değeri + yeni bir AddKeyedScoped satırıyla genişletilebiliyor — tam da bunun
// için tasarlanmıştı, aşağıdaki GarantiSandbox kaydı bunun ilk gerçek örneği.
builder.Services.AddKeyedScoped<IBankDataProvider, MockBankDataProvider>(Finara.Domain.Enums.BankDataProviderType.Mock);
builder.Services.AddScoped<IBankDataProviderFactory, BankDataProviderFactory>();

// Garanti BBVA geliştirici portalı (developers.garantibbva.com.tr) sandbox API'lerine
// bağlanan gerçek banka entegrasyonu — GarantiTokenProvider token'ı önbelleğe aldığı
// için Singleton, "Garanti" adlı named HttpClient üzerinden IHttpClientFactory ile
// paylaşılıyor (bkz. GarantiTokenProvider XML yorumu).
builder.Services.AddHttpClient("Garanti")
    .AddPolicyHandler(BuildHttpRetryPolicy())
    .AddPolicyHandler(BuildHttpCircuitBreakerPolicy());
builder.Services.Configure<GarantiApiOptions>(builder.Configuration.GetSection("Garanti"));
builder.Services.AddSingleton<IGarantiTokenProvider, GarantiTokenProvider>();
builder.Services.AddKeyedScoped<IBankDataProvider, GarantiBankDataProvider>(Finara.Domain.Enums.BankDataProviderType.GarantiSandbox);
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IBankService, BankService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ISegmentationService, SegmentationService>();
builder.Services.AddScoped<ICohortService, CohortService>();

// Faz 8.4 — IConsentGate (dar, sadece kontrol) ve IConsentService (Grant/Revoke) aynı
// sınıf tarafından implemente ediliyor, ama dispatcher'lar sadece IConsentGate'i
// enjekte ediyor (en az yetki ilkesi). Kanal başına dispatcher, BankDataProviderType
// ile aynı keyed-service deseniyle kayıtlı (bkz. yukarıdaki Faz 8.2 notu).
builder.Services.AddScoped<ConsentService>();
builder.Services.AddScoped<IConsentService>(sp => sp.GetRequiredService<ConsentService>());
builder.Services.AddScoped<IConsentGate>(sp => sp.GetRequiredService<ConsentService>());

// Hangi implementasyonun (Mock/gerçek sağlayıcı) her kanal için kayıtlı olacağı
// appsettings/user-secrets'taki Notifications:{Email,Sms}:Provider'a göre seçiliyor.
// Varsayılan HER ZAMAN Mock — kimlik bilgisi eksik/yanlış girildiğinde sessizce gerçek
// bir SMS/e-posta gönderilmesin (özellikle mock veri üretimi + test akışlarında).
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));

var emailProvider = builder.Configuration["Notifications:Email:Provider"] ?? "Mock";
if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddKeyedScoped<INotificationDispatcher, SmtpEmailDispatcher>(Finara.Domain.Enums.ConsentChannel.Email);
    builder.Services.AddScoped<IAdminEmailSender, SmtpAdminEmailSender>();
}
else
{
    builder.Services.AddKeyedScoped<INotificationDispatcher, MockEmailDispatcher>(Finara.Domain.Enums.ConsentChannel.Email);
    builder.Services.AddScoped<IAdminEmailSender, MockAdminEmailSender>();
}

var smsProvider = builder.Configuration["Notifications:Sms:Provider"] ?? "Mock";
if (string.Equals(smsProvider, "Twilio", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddKeyedScoped<INotificationDispatcher, TwilioSmsDispatcher>(Finara.Domain.Enums.ConsentChannel.Sms);
    Twilio.TwilioClient.Init(
        builder.Configuration["Twilio:AccountSid"],
        builder.Configuration["Twilio:AuthToken"]);
}
else
{
    builder.Services.AddKeyedScoped<INotificationDispatcher, MockSmsDispatcher>(Finara.Domain.Enums.ConsentChannel.Sms);
}

builder.Services.AddScoped<IOfferDistributionService, OfferDistributionService>();
builder.Services.AddScoped<INotificationLogService, NotificationLogService>();
builder.Services.AddScoped<INextBestOfferService, NextBestOfferService>();

// Bulut LLM API'si (ChatGPT/Claude) değil, kullanıcının kendi makinesinde
// çalışan Ollama (llama3.1:8b) — Faz 5. Tek bir IChatClient Singleton olarak
// kaydediliyor, AiChatService ve AiInsightService bunu paylaşıyor — öncesinde
// AiInsightService her çağrıda kendi client'ını oluşturup hiç Dispose etmiyordu
// (denetimde bulunan kaynak sızıntısı).
builder.Services.AddHttpClient("Ollama")
    .AddPolicyHandler(BuildHttpRetryPolicy())
    .AddPolicyHandler(BuildHttpCircuitBreakerPolicy());

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
    var model = config["Ollama:Model"] ?? "llama3.1:8b";
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    return new OllamaChatClient(new Uri(baseUrl), model, httpClient)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
});
builder.Services.AddSingleton<IAiChatService, AiChatService>();
builder.Services.AddScoped<IAiInsightService, AiInsightService>();

builder.Services.AddScoped<Finara.Infrastructure.Ai.CustomerContextTools>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Jwt:Key yapılandırılmamış. 'dotnet user-secrets set \"Jwt:Key\" \"<en az 32 karakter>\"' ile ekleyin.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // SignalR WebSocket bağlantıları Authorization header'ı taşıyamıyor — tarayıcı
        // istemcisi (accessTokenFactory) token'ı query string'e koyuyor, sadece hub
        // path'i için buradan okunuyor (diğer tüm REST endpoint'ler hâlâ header bekliyor).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// "WriteAccess" — Analist rolünün (salt-okunuz, bkz. UserRole.cs) hiçbir mutasyon
// (POST/PUT/DELETE) ucuna erişememesi için tüm yazma endpoint'lerinde kullanılıyor;
// GET uçları hâlâ sade RequireAuthorization() ile (herhangi bir kimliği doğrulanmış
// rol, Analist dahil) korunuyor. Rol claim'i zaten AuthService.IssueTokensAsync'te
// ClaimTypes.Role olarak ekleniyor, RequireRole bunu doğrudan okuyor.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WriteAccess", policy =>
        policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.BankAdmin)));
});

builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

// Angular dev server'ları buradan erişebilsin diye: client (4200) ve BackOffice (4300).
// Faz 8'de gerçek origin'ler appsettings üzerinden yapılandırılabilir hale getirilecek.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendApps", policy =>
        policy.WithOrigins("http://localhost:4200", "http://localhost:4300")
              .AllowAnyHeader()
              .AllowAnyMethod()
              // SignalR'ın negotiate/WebSocket el sıkışması için gerekli.
              .AllowCredentials());
});

// Enum'lar JSON'da okunabilir string olarak taşınsın ("Groceries"), ham int olarak değil —
// hem API tüketicisi için hem Swagger dokümantasyonu için gerekli.
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Finara API",
        Version = "v1",
        Description = "Harcama segmentasyonu ve kampanya önerisi için banka arka uç API'si (mock veri modu)."
    });
    options.SupportNonNullableReferenceTypes();
    // UseInlineDefinitionsForEnums BİLEREK kullanılmıyor: her kullanım yerinde ayrı bir
    // enum tanımı üretir (örn. CustomerSummaryPropensity, RecordTransactionRequestCategory),
    // bu da NSwag'ın aynı enum'u paylaşılan tek bir TypeScript tipi yerine tekrar tekrar
    // üretmesine yol açar. Paylaşılan $ref şeması için bu satır kapalı tutuluyor.
    options.SchemaFilter<Finara.Api.Swagger.EnumSchemaFilter>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "POST /api/auth/login ile alınan access token'ı buraya yapıştırın (sadece token, 'Bearer ' önekini Swagger ekliyor)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Kullanıcı/IP başına bölümlenmiş limitler — AddFixedWindowLimiter(policyName, ...)
    // kısayolu BİLEREK kullanılmıyor, o tüm istemciler için tek bir ortak sayaç kullanır
    // (bir istemcinin limiti doldurması diğer herkesi de kilitler). Bulundu ve düzeltildi:
    // denetimde anonim bir istek, başka bir kullanıcının doldurduğu kotadan 429 almıştı.

    // Genel uçlar: istemci başına dakikada 100 istek.
    options.AddPolicy(RateLimitPolicies.Global, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitPolicies.GetPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Toplu mock veri üretimi ağır bir işlem (çok sayıda insert) — istemci başına dakikada 5.
    options.AddPolicy(RateLimitPolicies.MockGeneration, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitPolicies.GetPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 6 haneli kod 1e6 kombinasyon — kaba kuvvete karşı Global'den (100/dk) çok daha sıkı
    // bir limit: istemci başına dakikada 5.
    options.AddPolicy(RateLimitPolicies.TwoFactorVerify, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitPolicies.GetPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Anonim, herkese açık, token üreten bir uç — kaba kuvvet riski yok (kimlik bilgisi
    // istemiyor) ama paylaşılan demo hesabında gereksiz RefreshToken satırı biriktiren
    // spam'e karşı Global'den (100/dk) daha sıkı: IP başına dakikada 15.
    options.AddPolicy(RateLimitPolicies.DemoLogin, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitPolicies.GetPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var id) ? id : null;
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Çok fazla istek",
            Detail = "İstek limiti aşıldı, lütfen bir süre sonra tekrar deneyin.",
            Instance = context.HttpContext.Request.Path
        };
        if (correlationId is not null)
            problemDetails.Extensions["correlationId"] = correlationId;

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    };
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
// Her isteğe tek satır, yapılandırılmış bir özet log kaydı (yol, durum kodu, süre) —
// CorrelationIdMiddleware'DEN SONRA kayıtlı olmalı ki CorrelationId scope'u içinde çalışsın
// ve bu özet satır da CorrelationId alanını taşısın.
app.UseSerilogRequestLogging();
app.UseMiddleware<PayloadGuardMiddleware>();
app.UseCors("AllowFrontendApps");
app.UseAuthentication();
app.UseAuthorization();
// UseAuthentication/UseAuthorization'dan SONRA çalışmalı — RateLimitPolicies.GetPartitionKey
// kullanıcı kimliğine göre bölümleme yapabilsin diye HttpContext.User'ın doldurulmuş
// olması gerekiyor. Öncesinde olsaydı her istek "authenticated değil" görünüp IP'ye düşerdi.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Finara API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => "Hello World!");

// Predicate = _ => false: hiçbir kayıtlı kontrolü çalıştırmaz, sadece HTTP host'un
// isteklere yanıt verebildiğini doğrular (klasik liveness probe).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Sadece "ready" etiketli kontrolleri (şu an: DB) çalıştırır — DB'ye erişemiyorsa
// 503 döner, orkestrasyon/monitoring bu instance'a trafik göndermemesi gerektiğini anlar.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
// Bilerek /health/ready'nin DIŞINDA — otomasyonun gecikmiş olması API'nin trafik almasını
// engellememeli, bu ayrı bir uç: harici bir izleme aracı (ör. ücretsiz bir uptime monitor)
// bunu ayrıca yoklayıp gerçek bir alarm kurabilir (bkz. AutomationHealthCheck'teki yorum).
app.MapHealthChecks("/health/automation", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("automation"),
    ResponseWriter = JsonHealthCheckResponseWriter.Write
});

app.MapAuthEndpoints();
app.MapCustomerEndpoints();
app.MapCampaignEndpoints();
app.MapAuditLogEndpoints();
app.MapAdminUserEndpoints();
app.MapBankEndpoints();
app.MapCohortEndpoints();
app.MapSegmentationEndpoints();
app.MapConsentEndpoints();
app.MapNotificationEndpoints();
app.MapReportEndpoints();
app.MapAiEndpoints();
// Denetimde bulundu: diğer her endpoint korunuyorken bu ikisi unutulmuştu — MCP
// üzerinden auth'suz olarak herhangi bir müşterinin verisi çekilebiliyordu.
app.MapMcp("/mcp")
    .RequireAuthorization()
    .RequireRateLimiting(RateLimitPolicies.Global);

app.MapHub<NotificationsHub>("/hubs/notifications").RequireAuthorization();

app.Run();
