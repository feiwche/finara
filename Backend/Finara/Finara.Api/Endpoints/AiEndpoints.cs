using Finara.Api.RateLimiting;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public record AiPingRequest(string Prompt);
public record AiPingResponse(string Response);

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        // Faz 5'in ilk doğrulama ucu: Ollama bağlantısının gerçekten çalıştığını kanıtlar.
        // MCP tool'ları eklendiğinde bunun yerini gerçek /api/customers/{id}/ai-insight alacak.
        app.MapPost("/api/ai/ping", async (AiPingRequest request, IAiChatService chatService, CancellationToken ct) =>
            Results.Ok(new AiPingResponse(await chatService.AskAsync(request.Prompt, ct))))
            .WithTags("AI")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization()
            .Produces<AiPingResponse>();
    }
}
