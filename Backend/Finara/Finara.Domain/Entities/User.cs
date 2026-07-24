using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// API'yi dışarıya karşı korumak için basit bir admin hesabı — bankacılık müşteri
// kimliği (Customer) ile bilerek karıştırılmadı, ayrı bir kavram.
public class User : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    // SuperAdmin için null (tüm bankaları görür), BankAdmin için zorunlu (bkz. Create).
    public Guid? BankId { get; private set; }

    // Haftalık özet e-postası (WeeklyDigestService) — null ise hiç gönderilmemiş demektir.
    // "Hangi gün gönderildi" gibi takvim bazlı bir kural yerine "son gönderimden bu yana
    // en az 7 gün geçti mi" kontrolü kullanılıyor — servis kesintiye uğrayıp yeniden
    // başlarsa (ör. bir Pazartesi kaçırılırsa) kırılgan olmasın diye.
    public DateTime? LastWeeklyDigestSentAtUtc { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, UserRole role = UserRole.SuperAdmin, Guid? bankId = null)
    {
        if (!IsValidEmail(email))
            throw new ArgumentException("Geçersiz e-posta formatı.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Şifre hash'i boş olamaz.", nameof(passwordHash));

        if (role is UserRole.BankAdmin or UserRole.Analyst && bankId is null)
            throw new ArgumentException($"{role} rolü bir banka kimliği gerektirir.", nameof(bankId));

        if (role == UserRole.SuperAdmin && bankId is not null)
            throw new ArgumentException("SuperAdmin bir bankaya bağlı olamaz.", nameof(bankId));

        return new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            BankId = bankId,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Kullanıcı zaten pasif.");

        IsActive = false;
        MarkUpdated();
    }

    public void MarkDigestSent()
    {
        LastWeeklyDigestSentAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void SetPasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Şifre hash'i boş olamaz.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        MarkUpdated();
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
