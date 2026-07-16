using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this WebApplication app)
    {
        var campaigns = app.MapGroup("/api/campaigns").WithTags("Campaigns")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        campaigns.MapPost("/", async (CreateCampaignRequest request, ICampaignService service, CancellationToken ct) =>
        {
            var summary = await service.CreateCampaignAsync(request, ct);
            return Results.Created($"/api/campaigns/{summary.Id}", summary);
        })
        .Produces<CampaignSummary>(StatusCodes.Status201Created);

        campaigns.MapGet("/", async (ICampaignService service, CancellationToken ct) =>
            Results.Ok(await service.GetCampaignsAsync(ct)))
            .Produces<IReadOnlyList<CampaignSummary>>();

        campaigns.MapPost("/{campaignId:guid}/activate", async (Guid campaignId, ICampaignService service, CancellationToken ct) =>
        {
            var summary = await service.ActivateCampaignAsync(campaignId, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Kampanya bulunamadı",
                    detail: $"'{campaignId}' kimlikli kampanya bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<CampaignSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
