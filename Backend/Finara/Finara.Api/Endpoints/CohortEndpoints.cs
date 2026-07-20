using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class CohortEndpoints
{
    public static void MapCohortEndpoints(this WebApplication app)
    {
        var cohorts = app.MapGroup("/api/cohorts").WithTags("Cohorts")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        cohorts.MapGet("/", async (ICohortService service, CancellationToken ct) =>
            Results.Ok(await service.GetCohortsAsync(ct)))
            .Produces<IReadOnlyList<CohortSummary>>();
    }
}
