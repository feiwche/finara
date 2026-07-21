using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class BankEndpoints
{
    public static void MapBankEndpoints(this WebApplication app)
    {
        var banks = app.MapGroup("/api/banks").WithTags("Banks")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        banks.MapGet("/", async (IBankService service, CancellationToken ct) =>
            Results.Ok(await service.GetBanksAsync(ct)))
            .Produces<IReadOnlyList<BankSummary>>();

        banks.MapPost("/", async (CreateBankRequest request, IBankService service, CancellationToken ct) =>
        {
            var summary = await service.CreateBankAsync(request, ct);
            return Results.Created($"/api/banks/{summary.Id}", summary);
        })
        .Produces<BankSummary>(StatusCodes.Status201Created);

        banks.MapPost("/{id:guid}/deactivate", async (Guid id, IBankService service, CancellationToken ct) =>
        {
            var summary = await service.DeactivateBankAsync(id, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Banka bulunamadı",
                    detail: $"'{id}' kimlikli banka bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<BankSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
