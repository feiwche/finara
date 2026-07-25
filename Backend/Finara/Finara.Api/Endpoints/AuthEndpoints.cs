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

        // Kendi tüm oturumlarını sonlandırma (tüm cihazlardan çıkış) — self-servis bir
        // hesap işlemi olduğu için WriteAccess DEĞİL, düz RequireAuthorization: Analyst
        // dahil her rol kendi oturumlarını kapatabilmeli.
        auth.MapPost("/revoke-all", async (ICurrentUserContext currentUser, IAuthService service, CancellationToken ct) =>
        {
            if (currentUser.UserId is not { } userId)
                return Results.Unauthorized();

            await service.RevokeAllAsync(userId, ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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

        // Path parametresi DEĞİL — davet token'ı base64 (RandomNumberGenerator çıktısı)
        // olduğu için "/" içerebiliyor, bu da ASP.NET Core'un route eşleştirmesini
        // (path segment'i böldüğü için) bozuyordu (canlı testte bulundu, 404). Query
        // string'te bu sorun yok çünkü değer path segmentasyonuna tabi değil.
        auth.MapGet("/invitations", async (string token, IAuthService service, CancellationToken ct) =>
        {
            var preview = await service.GetInvitationPreviewAsync(token, ct);
            return preview is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Davet bulunamadı",
                    detail: "Geçersiz veya süresi dolmuş davet bağlantısı.")
                : Results.Ok(preview);
        })
        .Produces<InvitationPreview>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        auth.MapPost("/accept-invitation", async (AcceptInvitationRequest request, IAuthService service, CancellationToken ct) =>
        {
            await service.AcceptInvitationAsync(request, ct);
            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent);
    }
}
