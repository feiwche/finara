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
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public bool HasInvestmentAccount { get; private set; }
    public bool HasInternationalTransfers { get; private set; }
    public SpendingPropensity CurrentPropensity { get; private set; } = SpendingPropensity.Neutral;

    private readonly List<Transaction> _transactions = new();
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    // EF Core, nesneleri veritabanından okurken parametresiz bir constructor'a ihtiyaç duyar.
    // 'private' yaparak bunu sadece EF Core'un kullanmasını sağlıyoruz, uygulama kodu kullanamaz.
    private Customer() { }

    public static Customer Create(string firstName, string lastName, string email, DateTime dateOfBirth)
    {
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

    public void AssignPropensity(SpendingPropensity propensity)
    {
        CurrentPropensity = propensity;
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
