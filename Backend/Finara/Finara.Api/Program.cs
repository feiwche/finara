using Finara.Application.Interfaces;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FinaraDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
