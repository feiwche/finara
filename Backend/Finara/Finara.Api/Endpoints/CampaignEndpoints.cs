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

        // Dört yaşam döngüsü ucu da aynı şekli paylaşıyor: servis çağrısı null dönerse
        // (kampanya yok) 404, aksi halde güncellenmiş özeti 200 ile döndür. Geçersiz durum
        // geçişleri (örn. taslak kampanyayı duraklatma) domain'de InvalidOperationException
        // fırlatır ve GlobalExceptionHandler tarafından 400'e çevrilir — burada ele alınmaz.
        static async Task<IResult> Transition(Guid campaignId, Func<Guid, CancellationToken, Task<CampaignSummary?>> action, CancellationToken ct)
        {
            var summary = await action(campaignId, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Kampanya bulunamadı",
                    detail: $"'{campaignId}' kimlikli kampanya bulunamadı.")
                : Results.Ok(summary);
        }

        campaigns.MapPost("/{campaignId:guid}/activate", (Guid campaignId, ICampaignService service, CancellationToken ct) =>
            Transition(campaignId, service.ActivateCampaignAsync, ct))
            .Produces<CampaignSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapPost("/{campaignId:guid}/pause", (Guid campaignId, ICampaignService service, CancellationToken ct) =>
            Transition(campaignId, service.PauseCampaignAsync, ct))
            .Produces<CampaignSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapPost("/{campaignId:guid}/complete", (Guid campaignId, ICampaignService service, CancellationToken ct) =>
            Transition(campaignId, service.CompleteCampaignAsync, ct))
            .Produces<CampaignSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapPost("/{campaignId:guid}/archive", (Guid campaignId, ICampaignService service, CancellationToken ct) =>
            Transition(campaignId, service.ArchiveCampaignAsync, ct))
            .Produces<CampaignSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
