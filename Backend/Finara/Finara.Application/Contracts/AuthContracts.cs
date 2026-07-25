using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record RevokeRequest(string RefreshToken);

public record AuthResult(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

// Login artık iki şekilde sonuçlanabilir: 2FA kapalıysa token alanları dolu/TwoFactorRequired
// false; açıksa token alanları null, TwoFactorRequired true + ChallengeToken dolu. NSwag'ın
// /markOptionalProperties + /nullValue:Undefined ayarı sayesinde TS tarafında temiz opsiyonel
// alanlar üretiyor, ayrı bir union tipine gerek yok.
public record LoginResult(
    string? AccessToken,
    DateTime? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc,
    bool TwoFactorRequired,
    string? ChallengeToken);

public record VerifyTwoFactorRequest(string ChallengeToken, string Code);

public record TwoFactorSetupResult(string Secret, string OtpAuthUri, string QrCodeDataUri);

public record ConfirmTwoFactorRequest(string Code);

public record DisableTwoFactorRequest(string Code);

// GET /api/auth/me — hesap sayfasının JWT claim'lerine güvenmeden taze durumu okuması için
// (access token 15 dk'ya kadar bayat kalabilir, özellikle enable/disable sonrası).
public record CurrentUserResult(Guid Id, string Email, UserRole Role, Guid? BankId, bool TwoFactorEnabled);

// ResetUrlBase, isteği gönderen frontend'in kendi origin'i (ör. http://localhost:4200) —
// client ve backoffice aynı User hesaplarıyla ayrı origin'lerden giriş yaptığı için backend
// tek bir sabit URL'e karar veremiyor. AuthService bunu izin verilen origin listesine karşı
// doğruluyor (açık yönlendirme/e-posta enjeksiyonunu önlemek için).
public record ForgotPasswordRequest(string Email, string ResetUrlBase);

public record ResetPasswordRequest(string Token, string NewPassword);

public record ForgotPasswordResult(string Message);

// Davet kabul sayfası, şifre alanını göstermeden önce kime davet gönderildiğini
// gösterebilsin diye (bkz. AuthService.GetInvitationPreviewAsync) — Role dahil,
// UI "BankAdmin/SuperAdmin olarak davet edildiniz" gibi bir bilgi gösterebilir.
public record InvitationPreview(string Email, UserRole Role);

public record AcceptInvitationRequest(string Token, string NewPassword);
