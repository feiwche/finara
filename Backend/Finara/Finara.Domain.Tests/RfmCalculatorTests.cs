using AwesomeAssertions;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Xunit;

namespace Finara.Domain.Tests;

public class RfmCalculatorTests
{
    private static readonly DateTime Reference = new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateScores_BosListe_BosSonucDoner()
    {
        var result = RfmCalculator.CalculateScores(Array.Empty<RfmCalculator.CustomerActivity>(), Reference);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateScores_TekMusteri_NotrSkorAlir()
    {
        var customerId = Guid.NewGuid();
        var activities = new[]
        {
            new RfmCalculator.CustomerActivity(customerId, Reference.AddDays(-1), 5, 1000m)
        };

        var result = RfmCalculator.CalculateScores(activities, Reference);

        result.Should().HaveCount(1);
        result[0].RecencyScore.Should().Be(3);
        result[0].FrequencyScore.Should().Be(3);
        result[0].MonetaryScore.Should().Be(3);
    }

    [Fact]
    public void CalculateScores_BesMusteri_EnYakinVeEnCokHarcayanEnYuksekSkoruAlir()
    {
        // 5 müşteri: biri en yakın zamanda + en sık + en çok harcayan, diğerleri kademeli azalıyor.
        var customers = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var activities = new[]
        {
            new RfmCalculator.CustomerActivity(customers[0], Reference.AddDays(-1), 20, 10000m),   // en iyi
            new RfmCalculator.CustomerActivity(customers[1], Reference.AddDays(-10), 15, 7000m),
            new RfmCalculator.CustomerActivity(customers[2], Reference.AddDays(-30), 10, 5000m),
            new RfmCalculator.CustomerActivity(customers[3], Reference.AddDays(-60), 5, 2000m),
            new RfmCalculator.CustomerActivity(customers[4], Reference.AddDays(-120), 1, 100m),     // en kötü
        };

        var result = RfmCalculator.CalculateScores(activities, Reference).ToDictionary(r => r.CustomerId);

        result[customers[0]].RecencyScore.Should().Be(5);
        result[customers[0]].FrequencyScore.Should().Be(5);
        result[customers[0]].MonetaryScore.Should().Be(5);

        result[customers[4]].RecencyScore.Should().Be(1);
        result[customers[4]].FrequencyScore.Should().Be(1);
        result[customers[4]].MonetaryScore.Should().Be(1);

        result[customers[0]].TotalScore.Should().Be(15);
        result[customers[4]].TotalScore.Should().Be(3);
    }

    [Theory]
    [InlineData(3, SpendingPropensity.NeedsEngagement)]
    [InlineData(6, SpendingPropensity.NeedsEngagement)]
    [InlineData(7, SpendingPropensity.Neutral)]
    [InlineData(11, SpendingPropensity.Neutral)]
    [InlineData(12, SpendingPropensity.HighlyResponsive)]
    [InlineData(15, SpendingPropensity.HighlyResponsive)]
    public void DeterminePropensity_SkorAraliklarinaGoreDogruEgilimiDoner(int totalScore, SpendingPropensity expected)
    {
        RfmCalculator.DeterminePropensity(totalScore).Should().Be(expected);
    }

    // Denetimde tespit edilen, ÜRÜN KARARIYLA korunan davranış: 5'ten az müşterili
    // popülasyonlarda quintile formülü gerçek NTILE(5)'ten sapıyor (bkz. RfmCalculator'daki
    // ayrıntılı gerekçe). Bu testler o sapmayı KİLİTLİYOR — biri formülü "düzeltirse" test
    // kırılır ve kararın bilinçli olduğu görülür; sessizce değişmez.
    [Fact]
    public void ScoreByQuintile_IkiMusteriliPopulasyon_Skorlari1ve3Olarak_Ayirir()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var scores = RfmCalculator.CalculateScores(
        [
            new RfmCalculator.CustomerActivity(a, Reference.AddDays(-1), 10, 100m),
            new RfmCalculator.CustomerActivity(b, Reference.AddDays(-1), 10, 900m)
        ], Reference);

        // Gerçek NTILE(5) 1 ve 2 verirdi; mevcut formül müşterileri 1-5 aralığına yayıyor.
        scores.Single(s => s.CustomerId == a).MonetaryScore.Should().Be(1);
        scores.Single(s => s.CustomerId == b).MonetaryScore.Should().Be(3);
    }

    [Fact]
    public void ScoreByQuintile_DortMusteriliPopulasyon_TavanSkor4Olur()
    {
        var customers = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        var scores = RfmCalculator.CalculateScores(
            customers.Select((id, i) => new RfmCalculator.CustomerActivity(id, Reference.AddDays(-1), 10, 100m * (i + 1))).ToList(),
            Reference);

        // 5 müşteriden azında en yüksek skor 5'e ULAŞMIYOR — bilinçli, belgelenmiş sapma.
        scores.Max(s => s.MonetaryScore).Should().Be(4);
        scores.Single(s => s.CustomerId == customers[0]).MonetaryScore.Should().Be(1);
    }
}
