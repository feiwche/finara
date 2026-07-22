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
        })
        .Produces(StatusCodes.Status201Created);

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
            Results.Ok(await service.LoginAsync(request, ct)))
            .Produces<AuthResult>();

        auth.MapPost("/refresh", async (RefreshRequest request, IAuthService service, CancellationToken ct) =>
            Results.Ok(await service.RefreshAsync(request, ct)))
            .Produces<AuthResult>();

        auth.MapPost("/revoke", async (RevokeRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.RevokeAsync(request, ct);
            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent);

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.ForgotPasswordAsync(request, ct);
            // Hesap var/yok bilgisi sızdırılmıyor — sonuç ne olursa olsun aynı jenerik yanıt.
            return Results.Ok(new ForgotPasswordResult("Bu e-posta adresine kayıtlı bir hesap varsa, şifre sıfırlama bağlantısı gönderildi."));
        })
        .Produces<ForgotPasswordResult>();

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.ResetPasswordAsync(request, ct);
            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent);
    }
}
