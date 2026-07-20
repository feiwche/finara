namespace Finara.Application.Interfaces;

// Faz 8.2 — hangi bankanın hangi IBankDataProvider implementasyonunu kullanacağını
// çözer (Bank.DataProviderType üzerinden). Tüketen kod (CustomerService) artık
// doğrudan MockBankDataProvider'a değil bu factory'ye bağımlı; gerçek bir banka
// entegrasyonu eklendiğinde sadece yeni implementasyon + DI kaydı yeterli olacak.
public interface IBankDataProviderFactory
{
    Task<IBankDataProvider> GetProviderAsync(Guid bankId, CancellationToken cancellationToken = default);
}
