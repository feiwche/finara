namespace Finara.Application.Interfaces;

// Banka hesap hareketi/müşteri verisi kaynağını soyutlar. Bugün tek implementasyonu
// Bogus ile sahte veri üreten MockBankDataProvider; ileride gerçek banka API'sine
// bağlanan bir implementasyon bu arayüzü karşılayarak eklenecek, tüketen kod değişmeyecek.
public interface IBankDataProvider
{
    Task<CustomerData> GenerateCustomerAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionData>> GenerateTransactionsAsync(int count, CancellationToken cancellationToken = default);
}
