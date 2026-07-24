using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports").WithTags("Reports")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        reports.MapGet("/offer-trend", async (int? days, IReportService service, CancellationToken ct) =>
            Results.Ok(await service.GetOfferTrendAsync(days ?? 30, ct)))
            .Produces<IReadOnlyList<DailyOfferTrendPoint>>();

        reports.MapGet("/customer-growth", async (int? days, IReportService service, CancellationToken ct) =>
            Results.Ok(await service.GetCustomerGrowthAsync(days ?? 30, ct)))
            .Produces<IReadOnlyList<DailyCustomerGrowthPoint>>();

        reports.MapGet("/offer-impact", async (IReportService service, CancellationToken ct) =>
            Results.Ok(await service.GetOfferImpactSummaryAsync(ct)))
            .Produces<OfferImpactSummary>();
    }
}
