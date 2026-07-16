using Finara.Application.Interfaces;
using Finara.Infrastructure.MockData;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FinaraDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Banka entegrasyonu henüz yok — tek implementasyon Bogus tabanlı mock üretici.
// İleride gerçek banka API'sine bağlanan bir implementasyon eklendiğinde sadece bu satır değişecek.
builder.Services.AddScoped<IBankDataProvider, MockBankDataProvider>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
