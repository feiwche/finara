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
}
