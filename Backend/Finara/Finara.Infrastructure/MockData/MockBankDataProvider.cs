using Bogus;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;

namespace Finara.Infrastructure.MockData;

public class MockBankDataProvider : IBankDataProvider
{
    private readonly Faker _faker = new("tr");

    // Kategori başına Türkiye pazarına özgü işyerleri — segmentasyon motorunun (Faz 4)
    // anlamlı örüntüler bulabilmesi için Groceries/Fuel/ECommerce gibi sık kategoriler
    // ağırlıklı listede daha çok tekrar ediyor.
    private static readonly IReadOnlyDictionary<TransactionCategory, string[]> MerchantsByCategory =
        new Dictionary<TransactionCategory, string[]>
        {
            [TransactionCategory.Groceries] = ["Migros", "Şok Market", "BİM", "CarrefourSA", "A101"],
            [TransactionCategory.Fuel] = ["Shell", "Opet", "BP", "Petrol Ofisi", "Total"],
            [TransactionCategory.Travel] = ["Turkish Airlines", "Pegasus", "Booking.com", "Enterprise Rent-A-Car"],
            [TransactionCategory.Entertainment] = ["Netflix", "Spotify", "Sinema Maximum", "Biletix"],
            [TransactionCategory.Healthcare] = ["Acıbadem", "Memorial Sağlık", "Eczane"],
            [TransactionCategory.ECommerce] = ["Trendyol", "Hepsiburada", "Amazon", "N11"],
            [TransactionCategory.Motorcycle] = ["Motosiklet Dünyası", "Kask Center", "Yamaha Yetkili Servis"],
            [TransactionCategory.Investment] = ["İş Yatırım", "Garanti Yatırım", "Midas"],
            [TransactionCategory.ForeignExchange] = ["Swift Transfer", "Wise", "Western Union"],
            [TransactionCategory.Utilities] = ["Türk Telekom", "Enerjisa", "İGDAŞ", "Turkcell"],
            [TransactionCategory.Other] = ["Genel Market", "Diğer"],
        };

    // Türkiye nüfusunun eğitim durumu dağılımına kabaca yakın ağırlıklandırma —
    // lise/lisans en yaygın, lisansüstü en seyrek.
    private static readonly EducationLevel[] WeightedEducationLevelPool =
    [
        EducationLevel.Lise, EducationLevel.Lise, EducationLevel.Lise, EducationLevel.Lise,
        EducationLevel.Lisans, EducationLevel.Lisans, EducationLevel.Lisans,
        EducationLevel.Onlisans, EducationLevel.Onlisans,
        EducationLevel.Ilkogretim, EducationLevel.Ilkogretim,
        EducationLevel.Lisansustu,
    ];

    // Sık kategoriler (market/akaryakıt/e-ticaret) ağırlıklı, nadir kategoriler (yatırım/motosiklet) seyrek.
    private static readonly TransactionCategory[] WeightedCategoryPool =
    [
        TransactionCategory.Groceries, TransactionCategory.Groceries, TransactionCategory.Groceries, TransactionCategory.Groceries, TransactionCategory.Groceries,
        TransactionCategory.Fuel, TransactionCategory.Fuel, TransactionCategory.Fuel,
        TransactionCategory.ECommerce, TransactionCategory.ECommerce, TransactionCategory.ECommerce, TransactionCategory.ECommerce,
        TransactionCategory.Entertainment, TransactionCategory.Entertainment, TransactionCategory.Entertainment,
        TransactionCategory.Utilities, TransactionCategory.Utilities, TransactionCategory.Utilities,
        TransactionCategory.Healthcare, TransactionCategory.Healthcare,
        TransactionCategory.Travel,
        TransactionCategory.Investment,
        TransactionCategory.ForeignExchange,
        TransactionCategory.Motorcycle,
        TransactionCategory.Other,
    ];

    public Task<CustomerData> GenerateCustomerAsync(CancellationToken cancellationToken = default)
    {
        var firstName = _faker.Name.FirstName();
        var lastName = _faker.Name.LastName();

        // Customer.Create() 18 yaşından küçüğü reddediyor — sınırın 1 gün içine düşmemesi için pay bırakılıyor.
        var dateOfBirth = _faker.Date.Between(
            DateTime.UtcNow.AddYears(-75),
            DateTime.UtcNow.AddYears(-18).AddDays(-1));

        var data = new CustomerData(
            FirstName: firstName,
            LastName: lastName,
            Email: _faker.Internet.Email(firstName, lastName),
            DateOfBirth: DateTime.SpecifyKind(dateOfBirth, DateTimeKind.Utc),
            HasInvestmentAccount: _faker.Random.Bool(0.2f),
            HasInternationalTransfers: _faker.Random.Bool(0.15f),
            EducationLevel: _faker.PickRandom(WeightedEducationLevelPool),
            // KVKK/İYS: gerçek dünyada rıza sorulmadan hiçbir kanal "var" sayılamaz —
            // mock veri de bunu simüle ediyor, kanal başına bağımsız bir olasılıkla.
            Consents: new Dictionary<ConsentChannel, bool>
            {
                [ConsentChannel.Sms] = _faker.Random.Bool(0.7f),
                [ConsentChannel.Email] = _faker.Random.Bool(0.6f),
                [ConsentChannel.InAppBanking] = _faker.Random.Bool(0.4f)
            });

        return Task.FromResult(data);
    }

    public Task<IReadOnlyList<TransactionData>> GenerateTransactionsAsync(int count, CancellationToken cancellationToken = default)
    {
        var transactions = new List<TransactionData>(count);

        for (var i = 0; i < count; i++)
        {
            var category = _faker.PickRandom(WeightedCategoryPool);
            var merchant = _faker.PickRandom(MerchantsByCategory[category]);
            var isInternational = category is TransactionCategory.ForeignExchange
                || (category is TransactionCategory.Travel && _faker.Random.Bool(0.3f));

            // Transaction.Create() gelecek tarihli işlemi reddediyor — üst sınıra pay bırakılıyor.
            var occurredAtUtc = _faker.Date.Between(
                DateTime.UtcNow.AddMonths(-6),
                DateTime.UtcNow.AddMinutes(-1));

            transactions.Add(new TransactionData(
                Amount: Math.Round(_faker.Random.Decimal(20, 5000), 2),
                Category: category,
                MerchantName: merchant,
                OccurredAtUtc: DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
                Currency: isInternational ? "USD" : "TRY",
                IsInternational: isInternational));
        }

        return Task.FromResult<IReadOnlyList<TransactionData>>(transactions);
    }
}
