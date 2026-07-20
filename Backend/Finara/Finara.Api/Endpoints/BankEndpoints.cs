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
    }
}
