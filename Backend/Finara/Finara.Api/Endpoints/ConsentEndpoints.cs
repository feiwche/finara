using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class ConsentEndpoints
{
    public static void MapConsentEndpoints(this WebApplication app)
    {
        var consents = app.MapGroup("/api/customers/{customerId:guid}/consents").WithTags("Consents")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        consents.MapGet("/", async (Guid customerId, IConsentService service, CancellationToken ct) =>
        {
            var result = await service.GetConsentsAsync(customerId, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<IReadOnlyList<ConsentSummary>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        consents.MapPut("/", async (Guid customerId, UpdateConsentRequest request, IConsentService service, CancellationToken ct) =>
        {
            var result = await service.UpdateConsentAsync(customerId, request, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(result);
        })
        .Produces<ConsentSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
