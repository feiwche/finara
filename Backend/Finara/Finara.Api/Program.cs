using System.Threading.RateLimiting;
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
    options.UseInlineDefinitionsForEnums();
    options.SchemaFilter<Finara.Api.Swagger.EnumSchemaFilter>();
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
app.MapCustomerEndpoints();

app.Run();
