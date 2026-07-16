using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/customers").WithTags("Customers").RequireRateLimiting(RateLimitPolicies.Global);

        customers.MapPost("/", async (CreateCustomerRequest request, ICustomerService service, CancellationToken ct) =>
        {
            var summary = await service.CreateCustomerAsync(request, ct);
            return Results.Created($"/api/customers/{summary.Id}", summary);
        });

        customers.MapGet("/", async (ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GetCustomersAsync(ct)));

        customers.MapPost("/{customerId:guid}/transactions", async (Guid customerId, RecordTransactionRequest request, ICustomerService service, CancellationToken ct) =>
        {
            var summary = await service.RecordTransactionAsync(customerId, request, ct);
            return summary is null
                ? Results.NotFound(new { message = $"'{customerId}' kimlikli müşteri bulunamadı." })
                : Results.Created($"/api/customers/{customerId}/transactions/{summary.Id}", summary);
        });

        customers.MapGet("/{customerId:guid}/transactions", async (Guid customerId, ICustomerService service, CancellationToken ct) =>
        {
            var transactions = await service.GetTransactionsAsync(customerId, ct);
            return transactions is null
                ? Results.NotFound(new { message = $"'{customerId}' kimlikli müşteri bulunamadı." })
                : Results.Ok(transactions);
        });

        app.MapPost("/api/mock/generate", async (GenerateMockDataRequest request, ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateMockDataAsync(request, ct)))
            .WithTags("Mock Data")
            .RequireRateLimiting(RateLimitPolicies.MockGeneration);
    }
}
