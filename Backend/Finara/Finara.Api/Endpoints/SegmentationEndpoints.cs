using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class SegmentationEndpoints
{
    public static void MapSegmentationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/segmentation/recalculate", async (ISegmentationService service, CancellationToken ct) =>
            Results.Ok(await service.RecalculateAllAsync(ct)))
            .WithTags("Segmentation")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization()
            .Produces<RecalculateSegmentsResult>();

        app.MapGet("/api/customers/{customerId:guid}/segment", async (Guid customerId, ISegmentationService service, CancellationToken ct) =>
        {
            var segment = await service.GetActiveSegmentAsync(customerId, ct);
            return segment is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Segment bulunamadı",
                    detail: $"'{customerId}' kimlikli müşterinin aktif bir segmenti yok. Önce POST /api/segmentation/recalculate çalıştırın.")
                : Results.Ok(segment);
        })
        .WithTags("Segmentation")
        .RequireRateLimiting(RateLimitPolicies.Global)
        .RequireAuthorization()
        .Produces<SegmentSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/customers/{customerId:guid}/segment/history", async (Guid customerId, ISegmentationService service, CancellationToken ct) =>
        {
            var history = await service.GetSegmentHistoryAsync(customerId, ct);
            return history is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(history);
        })
        .WithTags("Segmentation")
        .RequireRateLimiting(RateLimitPolicies.Global)
        .RequireAuthorization()
        .Produces<IReadOnlyList<SegmentSummary>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/customers/{customerId:guid}/segment/ai-commentary", async (Guid customerId, ISegmentationService service, CancellationToken ct) =>
        {
            var segment = await service.GenerateAiCommentaryAsync(customerId, ct);
            return segment is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Segment bulunamadı",
                    detail: $"'{customerId}' kimlikli müşterinin aktif bir segmenti yok.")
                : Results.Ok(segment);
        })
        .WithTags("Segmentation")
        .RequireRateLimiting(RateLimitPolicies.Global)
        .RequireAuthorization()
        .Produces<SegmentSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        var offers = app.MapGroup("/api/customers/{customerId:guid}/offers").WithTags("Offers")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        offers.MapPost("/generate", async (Guid customerId, int? maxOffers, INextBestOfferService service, CancellationToken ct) =>
        {
            var result = await service.GenerateOffersAsync(customerId, maxOffers ?? 5, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<GenerateOffersResult>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        offers.MapGet("/", async (Guid customerId, INextBestOfferService service, CancellationToken ct) =>
        {
            var result = await service.GetOffersAsync(customerId, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<IReadOnlyList<OfferSummary>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        offers.MapPost("/{offerId:guid}/accept", async (Guid customerId, Guid offerId, INextBestOfferService service, CancellationToken ct) =>
        {
            var result = await service.AcceptOfferAsync(customerId, offerId, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Teklif bulunamadı",
                    detail: $"'{offerId}' kimlikli teklif bu müşteri için bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<OfferSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        offers.MapPost("/{offerId:guid}/decline", async (Guid customerId, Guid offerId, INextBestOfferService service, CancellationToken ct) =>
        {
            var result = await service.DeclineOfferAsync(customerId, offerId, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Teklif bulunamadı",
                    detail: $"'{offerId}' kimlikli teklif bu müşteri için bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<OfferSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        offers.MapPost("/{offerId:guid}/distribute", async (Guid customerId, Guid offerId, DistributeOfferRequest request, IOfferDistributionService service, CancellationToken ct) =>
        {
            var result = await service.DistributeOfferAsync(customerId, offerId, request, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Teklif bulunamadı",
                    detail: $"'{offerId}' kimlikli teklif bu müşteri için bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<NotificationDispatchResult>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
