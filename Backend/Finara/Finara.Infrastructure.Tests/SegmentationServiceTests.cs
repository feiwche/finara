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
}
