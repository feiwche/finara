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

    // TOTP sırrı Data Protection ile şifrelenmiş halde saklanır (bkz. Infrastructure/Services/
    // TwoFactorService) — RefreshTokenHasher'daki "hash'le, asla geri çözme" deseninin aksine,
    // sunucunun her kod doğrulamasında düz metne geri dönmesi gerekiyor.
    public string? TwoFactorSecretEncrypted { get; private set; }

    // Kurulum akışı iki aşamalı: sır üretilip hemen saklanır ama TwoFactorEnabled true olana
    // kadar (kullanıcı en az bir geçerli kod girene kadar) devre dışı sayılır — yarım kalmış/
    // bozuk bir QR taramasının kullanıcıyı kilitli bir durumda bırakmaması için.
    public bool TwoFactorEnabled { get; private set; }

    public DateTime? TwoFactorEnabledAtUtc { get; private set; }

    // Dil/bildirim/tarih-biçimi/tablo yoğunluğu gibi kişisel arayüz tercihleri — bkz.
    // UserPreferences'taki JSON-kolon gerekçesi. Asla null değil (Create'te varsayılanla dolduruluyor).
    public UserPreferences Preferences { get; private set; } = new();

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

    public void SetPendingTwoFactorSecret(string encryptedSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            throw new ArgumentException("Şifrelenmiş sır boş olamaz.", nameof(encryptedSecret));

        // Yeniden kurulum başlatmak önceki sırrın üzerine yazar — eski, kullanılmayan bir sır
        // kalıcı bir güvenlik riski oluşturmaz, sadece atıl kalır. Etkinken sessizce yeni sır
        // üretilmesini engellemek servis katmanının sorumluluğu (önce DisableTwoFactor şart).
        TwoFactorSecretEncrypted = encryptedSecret;
        TwoFactorEnabled = false;
        TwoFactorEnabledAtUtc = null;
        MarkUpdated();
    }

    public void ConfirmTwoFactor()
    {
        if (TwoFactorSecretEncrypted is null)
            throw new InvalidOperationException("Önce bir sır üretilmeden iki faktörlü doğrulama onaylanamaz.");
        if (TwoFactorEnabled)
            throw new InvalidOperationException("İki faktörlü doğrulama zaten etkin.");

        TwoFactorEnabled = true;
        TwoFactorEnabledAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void DisableTwoFactor()
    {
        if (!TwoFactorEnabled)
            throw new InvalidOperationException("İki faktörlü doğrulama zaten devre dışı.");

        TwoFactorSecretEncrypted = null;
        TwoFactorEnabled = false;
        TwoFactorEnabledAtUtc = null;
        MarkUpdated();
    }

    public void UpdatePreferences(UserPreferences preferences)
    {
        if (preferences.DefaultPageSize is < 10 or > 100)
            throw new ArgumentException("Varsayılan sayfa boyutu 10 ile 100 arasında olmalı.", nameof(preferences));

        if (string.IsNullOrWhiteSpace(preferences.TimeZoneId))
            throw new ArgumentException("Zaman dilimi boş olamaz.", nameof(preferences));

        Preferences = preferences;
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
