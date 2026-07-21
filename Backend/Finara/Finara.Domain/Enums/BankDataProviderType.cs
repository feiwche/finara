namespace Finara.Domain.Enums;

// Faz 8.2 — bir bankanın müşteri/işlem verisini nereden aldığını belirler.
// Mock: Bogus tabanlı sahte veri. GarantiSandbox (bu faz eklendi): Garanti BBVA'nın
// geliştirici portalındaki (developers.garantibbva.com.tr) Account Information +
// Account Transactions sandbox API'lerinden gerçek (ama sandbox/mock) veri çeker —
// IBankDataProviderFactory'yi tüketen kod hiç değişmedi, tam da bunun için tasarlanmıştı.
public enum BankDataProviderType
{
    Mock,
    GarantiSandbox
}
