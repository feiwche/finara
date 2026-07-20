namespace Finara.Domain.Enums;

// Faz 8.2 — bir bankanın müşteri/işlem verisini nereden aldığını belirler.
// Bugün tek değer Mock (Bogus tabanlı sahte veri); gerçek bir banka entegrasyonu
// eklendiğinde yeni bir değer + IBankDataProvider implementasyonu + keyed DI kaydı
// eklenerek genişletilecek, IBankDataProviderFactory'yi tüketen kod değişmeyecek.
public enum BankDataProviderType
{
    Mock
}
