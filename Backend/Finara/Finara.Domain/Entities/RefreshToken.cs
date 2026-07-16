using Finara.Domain.Common;

namespace Finara.Domain.Entities;

// Access token (JWT) kısa ömürlü ve sunucuda hiç saklanmaz; refresh token ise
// opak, rastgele üretilmiş bir string olarak burada saklanır ki iptal edilebilsin
// (çalınan bir refresh token'ı geçersiz kılmanın tek yolu bu).
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Geçerli bir kullanıcı kimliği gerekli.", nameof(userId));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token boş olamaz.", nameof(token));

        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public void Revoke()
    {
        if (RevokedAtUtc is not null)
            throw new InvalidOperationException("Token zaten iptal edilmiş.");

        RevokedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }
}
