using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record RevokeRequest(string RefreshToken);

public record AuthResult(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

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
