using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

public class Customer : BaseEntity
{
    public Guid BankId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public bool HasInvestmentAccount { get; private set; }
    public bool HasInternationalTransfers { get; private set; }
    public EducationLevel? EducationLevel { get; private set; }
    public string? PhoneNumber { get; private set; }
    public SpendingPropensity CurrentPropensity { get; private set; } = SpendingPropensity.Neutral;

    // KVKK/GDPR "unutulma hakkı" — satır silinmiyor (Transaction/Segment/CampaignOffer gibi
    // Cascade FK'li geçmiş kayıtların referans bütünlüğü ve denetlenebilirlik için müşteri
    // yaşamaya devam etmeli), sadece doğrudan kimlik belirleyiciler (Erase() içinde) siliniyor.
    public bool IsErased { get; private set; }
    public DateTime? ErasedAtUtc { get; private set; }

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    // EF Core, nesneleri veritabanından okurken parametresiz bir constructor'a ihtiyaç duyar.
    // 'private' yaparak bunu sadece EF Core'un kullanmasını sağlıyoruz, uygulama kodu kullanamaz.
    private Customer() { }

    public static Customer Create(Guid bankId, string firstName, string lastName, string email, DateTime dateOfBirth)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("Geçerli bir banka kimliği gerekli.", nameof(bankId));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("Ad boş olamaz.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Soyad boş olamaz.", nameof(lastName));

        if (!IsValidEmail(email))
            throw new ArgumentException("Geçersiz e-posta formatı.", nameof(email));

        if (dateOfBirth > DateTime.UtcNow.AddYears(-18))
            throw new ArgumentException("Müşteri 18 yaşından küçük olamaz.", nameof(dateOfBirth));

        return new Customer
        {
            BankId = bankId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            DateOfBirth = dateOfBirth
        };
    }

    public int GetAge()
    {
        var today = DateTime.UtcNow;
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }

    public void UpdateFinancialFlags(bool hasInvestmentAccount, bool hasInternationalTransfers)
    {
        HasInvestmentAccount = hasInvestmentAccount;
        HasInternationalTransfers = hasInternationalTransfers;
        MarkUpdated();
    }

    public void UpdateEducationLevel(EducationLevel? educationLevel)
    {
        EducationLevel = educationLevel;
        MarkUpdated();
    }

    public void UpdatePhoneNumber(string? phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber) && !IsValidPhoneNumber(phoneNumber))
            throw new ArgumentException("Geçersiz telefon numarası formatı — E.164 bekleniyor (ör. +905551234567).", nameof(phoneNumber));

        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        MarkUpdated();
    }

    public void Erase()
    {
        if (IsErased)
            throw new InvalidOperationException("Müşteri zaten anonimleştirilmiş.");

        // Email için (BankId, Email) benzersizlik kısıtı hâlâ geçerli — kendi Id'sini
        // kullanmak başka bir anonimleştirilmiş müşteriyle çakışmayı imkansız kılıyor.
        FirstName = "Silinmiş";
        LastName = "Müşteri";
        Email = $"erased-{Id:N}@erased.finara.internal";
        PhoneNumber = null;
        EducationLevel = null;
        IsErased = true;
        ErasedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public AgeBracket GetAgeBracket() => GetAge() switch
    {
        <= 25 => AgeBracket.Age18To25,
        <= 35 => AgeBracket.Age26To35,
        <= 45 => AgeBracket.Age36To45,
        <= 55 => AgeBracket.Age46To55,
        _ => AgeBracket.Age56Plus
    };

    public void AssignPropensity(SpendingPropensity propensity)
    {
        CurrentPropensity = propensity;
        MarkUpdated();
    }

    // Twilio dahil çoğu SMS sağlayıcısı E.164 formatı bekliyor (+ülke kodu + numara,
    // toplam 8-15 rakam). Format burada, domain seviyesinde zorlanıyor — SMS dispatcher'ının
    // "numara var ama biçimi bozuk" gibi bir hataya kadar hiç ulaşmaması için.
    private static bool IsValidPhoneNumber(string phoneNumber) =>
        System.Text.RegularExpressions.Regex.IsMatch(phoneNumber.Trim(), @"^\+[1-9]\d{7,14}$");

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
