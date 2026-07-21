using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// Faz 8.4 — bir müşterinin bir kanal için verdiği açık rızayı takip eder. Müşteri
// başına kanal başına TEK satır (bkz. CustomerConsentConfiguration'daki (CustomerId,
// Channel) bileşik unique index) — geçmiş rıza değişiklikleri ayrı satırlar olarak değil,
// GrantedAtUtc/RevokedAtUtc üzerinden en son durum olarak tutuluyor (Segment'in
// aktif/pasif geçmişinden farklı, burada tam bir versiyon geçmişine gerek yok).
public class CustomerConsent : BaseEntity
{
    public Guid BankId { get; private set; }
    public Guid CustomerId { get; private set; }
    public ConsentChannel Channel { get; private set; }
    public bool IsGranted { get; private set; }
    public DateTime? GrantedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private CustomerConsent() { }

    public static CustomerConsent Create(Guid bankId, Guid customerId, ConsentChannel channel, bool isGranted)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("Geçerli bir banka kimliği gerekli.", nameof(bankId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        var now = DateTime.UtcNow;
        return new CustomerConsent
        {
            BankId = bankId,
            CustomerId = customerId,
            Channel = channel,
            IsGranted = isGranted,
            GrantedAtUtc = isGranted ? now : null,
            RevokedAtUtc = isGranted ? null : now
        };
    }

    public void Grant()
    {
        IsGranted = true;
        GrantedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Revoke()
    {
        IsGranted = false;
        RevokedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }
}
