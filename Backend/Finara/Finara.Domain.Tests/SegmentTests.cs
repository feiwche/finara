using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class SegmentTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliSkorlarla_SegmentOlusturur()
    {
        var segment = Domain.Entities.Segment.Create(
            ValidCustomerId, recencyScore: 5, frequencyScore: 4, monetaryScore: 3,
            SpendingPropensity.HighlyResponsive, TransactionCategory.Travel);

        segment.RecencyScore.Should().Be(5);
        segment.FrequencyScore.Should().Be(4);
        segment.MonetaryScore.Should().Be(3);
        segment.DominantCategory.Should().Be(TransactionCategory.Travel);
        segment.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_BosMusteriId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Segment.Create(
            Guid.Empty, 3, 3, 3, SpendingPropensity.Neutral);

        act.Should().Throw<ArgumentException>().WithParameterName("customerId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_GecersizRecencySkoru_ArgumentOutOfRangeFirlatir(int score)
    {
        var act = () => Domain.Entities.Segment.Create(
            ValidCustomerId, score, 3, 3, SpendingPropensity.Neutral);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("recencyScore");
    }

    [Fact]
    public void GetTotalScore_UcSkorunToplaminiDondurur()
    {
        var segment = Domain.Entities.Segment.Create(
            ValidCustomerId, 5, 4, 3, SpendingPropensity.HighlyResponsive);

        segment.GetTotalScore().Should().Be(12);
    }

    [Fact]
    public void Deactivate_AktifBayraginiKapatir()
    {
        var segment = Domain.Entities.Segment.Create(
            ValidCustomerId, 3, 3, 3, SpendingPropensity.Neutral);

        segment.Deactivate();

        segment.IsActive.Should().BeFalse();
    }
}
