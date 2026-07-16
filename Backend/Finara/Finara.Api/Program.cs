using Finara.Api.ExceptionHandling;
using Finara.Api.Middleware;
using Finara.Application.Interfaces;
using Finara.Infrastructure.MockData;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddDbContext<FinaraDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Banka entegrasyonu henüz yok — tek implementasyon Bogus tabanlı mock üretici.
// İleride gerçek banka API'sine bağlanan bir implementasyon eklendiğinde sadece bu satır değişecek.
builder.Services.AddScoped<IBankDataProvider, MockBankDataProvider>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Finara API",
        Version = "v1",
        Description = "Harcama segmentasyonu ve kampanya önerisi için banka arka uç API'si (mock veri modu)."
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

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

app.Run();
