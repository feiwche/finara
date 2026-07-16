using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Kimlik doğrulama uçlarının kendisi doğal olarak RequireAuthorization almıyor —
        // ama toplu deneme (brute-force) riskine karşı rate limiting hâlâ uygulanıyor.
        var auth = app.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting(RateLimitPolicies.Global);

        auth.MapPost("/register", async (RegisterRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.RegisterAsync(request, ct);
            return Results.Created();
        });

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
            Results.Ok(await service.LoginAsync(request, ct)));

        auth.MapPost("/refresh", async (RefreshRequest request, IAuthService service, CancellationToken ct) =>
            Results.Ok(await service.RefreshAsync(request, ct)));

        auth.MapPost("/revoke", async (RevokeRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(request, ct);
            return Results.NoContent();
        });
    }
}
