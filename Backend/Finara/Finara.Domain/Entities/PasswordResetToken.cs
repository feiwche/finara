using Finara.Domain.Common;

namespace Finara.Domain.Entities;

// RefreshToken ile aynı desen (ham değer sadece kullanıcıya/e-postaya gider, DB'de
// sadece hash — bkz. RefreshTokenHasher, burada da reuse ediliyor). Tek fark: tek
// kullanımlık olması (UsedAtUtc) — bir refresh token gibi rotasyonla değil, ilk
// kullanımda kalıcı olarak tükeniyor.
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public bool IsActive => UsedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Geçerli bir kullanıcı kimliği gerekli.", nameof(userId));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash'i boş olamaz.", nameof(tokenHash));

        return new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public void MarkUsed()
    {
        if (UsedAtUtc is not null)
            throw new InvalidOperationException("Token zaten kullanılmış.");

        UsedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }
}
