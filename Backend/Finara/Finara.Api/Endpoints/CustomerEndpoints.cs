using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var customers = app.MapGroup("/api/customers").WithTags("Customers")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        customers.MapPost("/", async (CreateCustomerRequest request, ICustomerService service, CancellationToken ct) =>
        {
            var summary = await service.CreateCustomerAsync(request, ct);
            return Results.Created($"/api/customers/{summary.Id}", summary);
        })
        .Produces<CustomerSummary>(StatusCodes.Status201Created)
        .RequireAuthorization("WriteAccess");

        customers.MapGet("/", async (ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GetCustomersAsync(ct)))
            .Produces<IReadOnlyList<CustomerSummary>>();

        // Tekil müşteri — detay ekranı önceden tüm listeyi çekip istemcide filtreliyordu.
        customers.MapGet("/{customerId:guid}", async (Guid customerId, ICustomerService service, CancellationToken ct) =>
        {
            var customer = await service.GetCustomerByIdAsync(customerId, ct);
            return customer is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(customer);
        })
        .Produces<CustomerSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Dashboard/segment/teklif hedefleme gibi "tüm müşteriler" gerektiren tüketiciler
        // hâlâ yukarıdaki "/" ucunu kullanıyor (bilinçli olarak dokunulmadı) — bu, Müşteriler
        // ekranının kendi liste tablosu için ayrı, sunucu taraflı sayfalanan bir uç.
        customers.MapGet("/paged", async (int? page, int? pageSize, string? search, ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GetCustomersPagedAsync(page ?? 1, pageSize ?? 20, search, ct)))
            .Produces<PagedResult<CustomerSummary>>();

        customers.MapGet("/stats", async (ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GetCustomerStatsAsync(ct)))
            .Produces<CustomerStats>();

        customers.MapPost("/import", async (IFormFile file, ICustomerService service, CancellationToken ct) =>
        {
            const long maxCsvSizeBytes = 5 * 1024 * 1024;
            if (file.Length > maxCsvSizeBytes)
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Dosya çok büyük",
                    detail: $"CSV dosyası izin verilen {maxCsvSizeBytes / 1024 / 1024} MB sınırını aşıyor.");

            await using var stream = file.OpenReadStream();
            var result = await service.ImportCustomersAsync(stream, ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<CustomerImportResult>()
        .RequireAuthorization("WriteAccess");

        customers.MapPost("/{customerId:guid}/erase", async (Guid customerId, ICustomerService service, CancellationToken ct) =>
        {
            var summary = await service.EraseCustomerAsync(customerId, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<CustomerSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("WriteAccess");

        customers.MapPost("/{customerId:guid}/transactions", async (Guid customerId, RecordTransactionRequest request, ICustomerService service, CancellationToken ct) =>
        {
            var summary = await service.RecordTransactionAsync(customerId, request, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Created($"/api/customers/{customerId}/transactions/{summary.Id}", summary);
        })
        .Produces<TransactionSummary>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("WriteAccess");

        customers.MapGet("/{customerId:guid}/transactions", async (Guid customerId, ICustomerService service, CancellationToken ct) =>
        {
            var transactions = await service.GetTransactionsAsync(customerId, ct);
            return transactions is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(transactions);
        })
        .Produces<IReadOnlyList<TransactionSummary>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/mock/generate", async (GenerateMockDataRequest request, ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateMockDataAsync(request, ct)))
            .WithTags("Mock Data")
            .RequireRateLimiting(RateLimitPolicies.MockGeneration)
            .RequireAuthorization("WriteAccess")
            .Produces<MockGenerationResult>();
    }
}
