using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// PasswordResetToken ile aynı desen (ham değer sadece e-postaya gider, DB'de sadece
// hash) — ama kabul edildiğinde bir User'a dönüşüyor, var olan bir User'a değil.
// Rol/BankId kuralları User.Create ile birebir aynı (BankAdmin->BankId zorunlu,
// SuperAdmin->BankId yasak) — AcceptInvitationAsync'te User.Create'e olduğu gibi geçiyor.
public class UserInvitation : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? BankId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid InvitedByUserId { get; private set; }

    public bool IsPending => AcceptedAtUtc is null && RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private UserInvitation() { }

    public static UserInvitation Create(
        string email,
        UserRole role,
        Guid? bankId,
        string tokenHash,
        DateTime expiresAtUtc,
        Guid invitedByUserId)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-posta boş olamaz.", nameof(email));

        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash'i boş olamaz.", nameof(tokenHash));

        if (role == UserRole.BankAdmin && bankId is null)
            throw new ArgumentException("BankAdmin rolü bir banka kimliği gerektirir.", nameof(bankId));

        if (role == UserRole.SuperAdmin && bankId is not null)
            throw new ArgumentException("SuperAdmin bir bankaya bağlı olamaz.", nameof(bankId));

        return new UserInvitation
        {
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            BankId = bankId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            InvitedByUserId = invitedByUserId
        };
    }

    public void Revoke()
    {
        if (AcceptedAtUtc is not null)
            throw new DomainException(ErrorCodes.UserInvitationAlreadyAccepted, "Kabul edilmiş bir davet iptal edilemez.");
        if (RevokedAtUtc is not null)
            throw new DomainException(ErrorCodes.UserInvitationAlreadyRevoked, "Davet zaten iptal edilmiş.");

        RevokedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }
}
