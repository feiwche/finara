using AwesomeAssertions;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Xunit;

namespace Finara.Domain.Tests;

public class CohortEngineTests
{
    private static CohortEngine.CustomerCohortInput Input(
        AgeBracket ageBracket,
        EducationLevel? educationLevel,
        SpendingPropensity propensity,
        TransactionCategory? dominantCategory = null) =>
        new(Guid.NewGuid(), ageBracket, educationLevel, propensity, dominantCategory);

    [Fact]
    public void Compute_BosListe_BosSonucDoner()
    {
        var result = CohortEngine.Compute(Array.Empty<CohortEngine.CustomerCohortInput>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compute_EsikUstuGrup_AyriKohortOlarakDoner()
    {
        var inputs = Enumerable.Range(0, 5)
            .Select(_ => Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, TransactionCategory.Travel))
            .ToList();

        var result = CohortEngine.Compute(inputs, minCohortSize: 5);

        result.Should().HaveCount(1);
        result[0].IsOther.Should().BeFalse();
        result[0].AgeBracket.Should().Be(AgeBracket.Age26To35);
        result[0].EducationLevel.Should().Be(EducationLevel.Lisans);
        result[0].Propensity.Should().Be(SpendingPropensity.HighlyResponsive);
        result[0].DominantCategory.Should().Be(TransactionCategory.Travel);
        result[0].CustomerCount.Should().Be(5);
    }

    [Fact]
    public void Compute_EsikAltiGruplar_DigerKovasindaBirlesir()
    {
        // 3 farklı kombinasyon, her biri 1-2 müşteri — hiçbiri eşiği (5) geçmiyor.
        var inputs = new[]
        {
            Input(AgeBracket.Age18To25, EducationLevel.Lise, SpendingPropensity.Neutral),
            Input(AgeBracket.Age18To25, EducationLevel.Lise, SpendingPropensity.Neutral),
            Input(AgeBracket.Age56Plus, EducationLevel.Lisansustu, SpendingPropensity.NeedsEngagement),
            Input(AgeBracket.Age36To45, null, SpendingPropensity.HighlyResponsive)
        };

        var result = CohortEngine.Compute(inputs, minCohortSize: 5);

        result.Should().HaveCount(1);
        result[0].IsOther.Should().BeTrue();
        result[0].AgeBracket.Should().BeNull();
        result[0].EducationLevel.Should().BeNull();
        result[0].Propensity.Should().BeNull();
        result[0].CustomerCount.Should().Be(4);
    }

    [Fact]
    public void Compute_KarisikPopulasyon_BuyukKohortlarAyriKucuklerDigerdeToplanir()
    {
        var inputs = new List<CohortEngine.CustomerCohortInput>();
        inputs.AddRange(Enumerable.Range(0, 6)
            .Select(_ => Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive)));
        inputs.Add(Input(AgeBracket.Age46To55, EducationLevel.Onlisans, SpendingPropensity.Neutral));

        var result = CohortEngine.Compute(inputs, minCohortSize: 5);

        result.Should().HaveCount(2);
        result[0].CustomerCount.Should().Be(6);
        result[0].IsOther.Should().BeFalse();
        result[1].CustomerCount.Should().Be(1);
        result[1].IsOther.Should().BeTrue();
    }

    [Fact]
    public void Compute_BaskinKategori_EnCokTekrarEdenKategoriSecilir()
    {
        var inputs = new[]
        {
            Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, TransactionCategory.Travel),
            Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, TransactionCategory.Travel),
            Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, TransactionCategory.Groceries),
            Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, TransactionCategory.Travel),
            Input(AgeBracket.Age26To35, EducationLevel.Lisans, SpendingPropensity.HighlyResponsive, null)
        };

        var result = CohortEngine.Compute(inputs, minCohortSize: 5);

        result.Should().HaveCount(1);
        result[0].DominantCategory.Should().Be(TransactionCategory.Travel);
    }
}
