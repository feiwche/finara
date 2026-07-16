using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Finara.Api.Endpoints;
using Finara.Api.ExceptionHandling;
using Finara.Api.Middleware;
using Finara.Api.RateLimiting;
using Finara.Application.Interfaces;
using Finara.Infrastructure.MockData;
using Finara.Infrastructure.Persistence;
using Finara.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<FinaraDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.Configure<PayloadGuardOptions>(builder.Configuration.GetSection("PayloadGuard"));

// Banka entegrasyonu henüz yok — tek implementasyon Bogus tabanlı mock üretici.
// İleride gerçek banka API'sine bağlanan bir implementasyon eklendiğinde sadece bu satır değişecek.
builder.Services.AddScoped<IBankDataProvider, MockBankDataProvider>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAuthService, AuthService>();

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
    });

builder.Services.AddAuthorization();

// Faz 3.5: Angular dev server (ng serve, varsayılan 4200) buradan erişebilsin diye.
// Faz 6/7'de gerçek origin'ler appsettings üzerinden yapılandırılabilir hale getirilecek.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
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

    // Genel uçlar: dakikada 100 istek — normal CRUD trafiği için yeterince gevşek.
    options.AddFixedWindowLimiter(RateLimitPolicies.Global, opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Toplu mock veri üretimi ağır bir işlem (çok sayıda insert) — dakikada 5 ile sınırlı.
    options.AddFixedWindowLimiter(RateLimitPolicies.MockGeneration, opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

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
app.UseMiddleware<PayloadGuardMiddleware>();
app.UseRateLimiter();
app.UseCors("AllowAngularDev");
app.UseAuthentication();
app.UseAuthorization();

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
app.MapAuthEndpoints();
app.MapCustomerEndpoints();

app.Run();
