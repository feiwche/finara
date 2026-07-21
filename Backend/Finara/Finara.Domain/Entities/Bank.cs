using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

// Faz 8.1 — multi-tenancy temeli. Gerçek ikinci bir banka entegrasyonu henüz yok,
// ama veri modeli (Customer/Transaction/Segment/Campaign/CampaignOffer/AuditLog/User
// hepsi BankId taşıyor) buna hazır. Bkz. Finara.Domain.Common.DefaultBank — mevcut
// tek banka bu sabit kimlikle seed ediliyor, geriye dönük tüm veriler ona bağlanıyor.
public class Bank : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Faz 8.2 — IBankDataProviderFactory'nin hangi IBankDataProvider implementasyonunu
    // çözeceğini belirler. Bugün her banka Mock ile oluşturuluyor (gerçek entegrasyon yok).
    public BankDataProviderType DataProviderType { get; private set; } = BankDataProviderType.Mock;

    private Bank() { }

    public static Bank Create(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Banka adı boş olamaz.", nameof(name));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Banka kodu boş olamaz.", nameof(code));

        return new Bank
        {
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            IsActive = true,
            DataProviderType = BankDataProviderType.Mock
        };
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Banka zaten pasif.");

        IsActive = false;
        MarkUpdated();
    }

    public void SetDataProviderType(BankDataProviderType dataProviderType)
    {
        DataProviderType = dataProviderType;
        MarkUpdated();
    }
}
