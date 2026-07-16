using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public record AiPingRequest(string Prompt);
public record AiPingResponse(string Response);

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        // Faz 5'in ilk doğrulama ucu — Ollama bağlantısının çalıştığını kanıtlamak için
        // eklenmişti, hâlâ hızlı bir bağlantı testi olarak faydalı, kalıyor.
        app.MapPost("/api/ai/ping", async (AiPingRequest request, IAiChatService chatService, CancellationToken ct) =>
            Results.Ok(new AiPingResponse(await chatService.AskAsync(request.Prompt, ct))))
            .WithTags("AI")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization()
            .Produces<AiPingResponse>();

        app.MapPost("/api/customers/{customerId:guid}/ai-insight", async (Guid customerId, AiInsightRequest request, IAiInsightService service, CancellationToken ct) =>
        {
            var result = await service.GenerateInsightAsync(customerId, request.Question, ct);
            return result is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Müşteri bulunamadı",
                    detail: $"'{customerId}' kimlikli müşteri bulunamadı.")
                : Results.Ok(result);
        })
        .WithTags("AI")
        // Ollama isteği saniyeler sürebiliyor — genel limitten daha sıkı bir politika mantıklı olurdu
        // ama Faz 5 kapsamını dar tutmak için şimdilik genel limiti kullanıyoruz.
        .RequireRateLimiting(RateLimitPolicies.Global)
        .RequireAuthorization()
        .Produces<AiInsightResult>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
