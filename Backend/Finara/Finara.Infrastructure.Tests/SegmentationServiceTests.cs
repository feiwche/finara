using AwesomeAssertions;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class SegmentationServiceTests : SegmentationServiceTestBase
{
    // Bu test, düzeltilmeden önce gerçekten kırmızı çıkan bir regresyonu doğruluyor: otomatik
    // segmentasyon döngüsü (SuperAdmin kimliğiyle) TÜM bankaları TEK bir RFM popülasyonunda
    // karıştırıyordu. Banka B'nin müşterileri çok daha yüksek tutarlarla işlem yapıyor —
    // eğer popülasyonlar karışsaydı Banka A'nın en yüksek harcayan müşterisi bile Banka B'nin
    // devasa tutarları yanında "düşük" kalıp en kötü Monetary skorunu (1) alırdı. Banka bazlı
    // izolasyon doğruysa, Banka A KENDİ popülasyonu içinde değerlendirilir ve o popülasyonun
    // en yüksek harcayanı en iyi skoru (5) alır — Banka B'nin varlığından habersiz.
    [Fact]
    public async Task RecalculateAllAsync_FarkliBankalardaFarkliHarcamaHacimleriVarken_HerBankaKendiPopulasyonundaSkorlanir()
    {
        var now = DateTime.UtcNow;

        // Banka A: mütevazı tutarlar (10-50 TRY), 5 müşteri.
        var bankACustomers = Enumerable.Range(1, 5)
            .Select(i => CreateCustomer(BankAId, $"bankA-{i}@example.com"))
            .ToList();
        for (var i = 0; i < bankACustomers.Count; i++)
            AddTransaction(bankACustomers[i], BankAId, amount: 10m * (i + 1), occurredAtUtc: now.AddDays(-5));

        // Banka B: çok daha büyük tutarlar (10.000-50.000 TRY), 5 müşteri — popülasyonlar
        // karışsaydı Banka A'nın tamamını "düşük harcayan" tarafa iterdi.
        var bankBCustomers = Enumerable.Range(1, 5)
            .Select(i => CreateCustomer(BankBId, $"bankB-{i}@example.com"))
            .ToList();
        for (var i = 0; i < bankBCustomers.Count; i++)
            AddTransaction(bankBCustomers[i], BankBId, amount: 10_000m * (i + 1), occurredAtUtc: now.AddDays(-5));

        DbContext.SaveChanges();

        await Sut.RecalculateAllAsync();

        // Banka A'nın en yüksek harcayanı (50 TRY) KENDİ popülasyonunda en iyi Monetary
        // skorunu (5) almalı — Banka B'nin 50.000 TRY'lik müşterisi bunu etkilememeli.
        var topBankACustomerId = bankACustomers[4].Id;
        var topBankASegment = DbContext.Segments.Single(s => s.CustomerId == topBankACustomerId && s.IsActive);
        topBankASegment.MonetaryScore.Should().Be(5);

        // Aynı şekilde Banka A'nın en düşük harcayanı (10 TRY) kendi popülasyonunda en kötü
        // skoru (1) almalı.
        var bottomBankACustomerId = bankACustomers[0].Id;
        var bottomBankASegment = DbContext.Segments.Single(s => s.CustomerId == bottomBankACustomerId && s.IsActive);
        bottomBankASegment.MonetaryScore.Should().Be(1);

        // Banka B'nin kendi en yüksek harcayanı da kendi popülasyonunda en iyi skoru almalı.
        var topBankBCustomerId = bankBCustomers[4].Id;
        var topBankBSegment = DbContext.Segments.Single(s => s.CustomerId == topBankBCustomerId && s.IsActive);
        topBankBSegment.MonetaryScore.Should().Be(5);
    }

    // Denetimde bulunan hata: farklı para birimlerindeki tutarlar veritabanında doğrudan
    // toplanıyordu, yani 1 USD = 1 TRY sayılıyordu. Bu, yurtdışı transfer yapan (USD harcayan)
    // müşterileri — yani yatırım/SWIFT kampanyalarının hedef kitlesini — sistematik olarak
    // olduğundan düşük skorluyordu.
    //
    // Senaryo: dört müşteriden üçü sırasıyla 100/200/300 TRY harcıyor, dördüncüsü 100 USD
    // harcıyor. Çevrim yapılmazsa 100 USD = 100 TRY sayılıp EN DÜŞÜK skoru alır; doğru
    // davranışta 100 USD = 3500 TRY ile popülasyonun EN YÜKSEĞİ olup en iyi skoru almalı.
    [Fact]
    public async Task RecalculateAllAsync_YabanciParaBirimliHarcama_TemelParaBirimineCevrilerekSkorlanir()
    {
        var now = DateTime.UtcNow;

        var tryCustomers = Enumerable.Range(1, 3)
            .Select(i => CreateCustomer(BankAId, $"try-{i}@example.com"))
            .ToList();
        for (var i = 0; i < tryCustomers.Count; i++)
            AddTransaction(tryCustomers[i], BankAId, amount: 100m * (i + 1), occurredAtUtc: now.AddDays(-5));

        var usdCustomer = CreateCustomer(BankAId, "usd@example.com");
        AddTransaction(usdCustomer, BankAId, amount: 100m, occurredAtUtc: now.AddDays(-5), currency: "USD");

        DbContext.SaveChanges();

        await Sut.RecalculateAllAsync();

        // 100 USD = 3500 TRY → popülasyonun EN YÜKSEĞİ olmalı. Mutlak skor değeri yerine
        // SIRALAMA doğrulanıyor: RfmCalculator'ın quintile formülü n<5 popülasyonlarda en
        // yüksek skoru 5'e çıkarmıyor (4 müşteride tavan 4) — bu ayrı, bilinen bir konu;
        // burada test edilen şey para birimi çevriminin sıralamayı düzeltip düzeltmediği.
        var usdSegment = DbContext.Segments.Single(s => s.CustomerId == usdCustomer.Id && s.IsActive);
        var trySegments = tryCustomers
            .Select(c => DbContext.Segments.Single(s => s.CustomerId == c.Id && s.IsActive))
            .ToList();

        usdSegment.MonetaryScore.Should().BeGreaterThan(trySegments.Max(s => s.MonetaryScore));

        // Çevrim yapılmasaydı 100 USD = 100 TRY sayılıp EN DÜŞÜK skoru alırdı — bu kontrol
        // tam olarak o regresyonu yakalar.
        var topTrySegment = DbContext.Segments.Single(s => s.CustomerId == tryCustomers[2].Id && s.IsActive);
        topTrySegment.MonetaryScore.Should().BeLessThan(usdSegment.MonetaryScore);
    }
}
