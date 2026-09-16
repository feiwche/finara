using AwesomeAssertions;
using Finara.Domain.Services;
using Xunit;

namespace Finara.Domain.Tests;

public class CurrencyConverterTests
{
    private static CurrencyConverter CreateSut() =>
        new("TRY", new Dictionary<string, decimal> { ["USD"] = 35m, ["EUR"] = 38m });

    [Fact]
    public void ToBase_TemelParaBirimi_DegisiklikYapmadanDoner()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "TRY").Should().Be(1000m);
    }

    [Fact]
    public void ToBase_TanimliKur_DogruCarpanlaDonusturur()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "USD").Should().Be(35_000m);
        sut.ToBase(1000m, "EUR").Should().Be(38_000m);
    }

    [Fact]
    public void ToBase_KucukHarfliParaBirimi_BuyukHarfliyleAyniSonucuVerir()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "usd").Should().Be(sut.ToBase(1000m, "USD"));
    }

    [Fact]
    public void ToBase_BaslindaSonundaBosluklu_TrimlenerekDogruDonusturulur()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "  USD  ").Should().Be(35_000m);
    }

    // Bilinçli tasarım kararı (bkz. CurrencyConverter'daki sınıf yorumu): tanınmayan bir para
    // birimi yok sayılmak yerine 1:1 kabul edilir. Bu test o davranışı kilitliyor.
    [Fact]
    public void ToBase_TanimsizParaBirimi_BirBireBirVarsayilirak_TutarDegismedenDoner()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "GBP").Should().Be(1000m);
    }

    [Fact]
    public void ToBase_NullParaBirimi_TutarDegismedenDoner()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, null).Should().Be(1000m);
    }

    [Fact]
    public void ToBase_BosVeyaBeyazBoslukParaBirimi_TutarDegismedenDoner()
    {
        var sut = CreateSut();

        sut.ToBase(1000m, "").Should().Be(1000m);
        sut.ToBase(1000m, "   ").Should().Be(1000m);
    }

    [Fact]
    public void ToBase_SifirTutar_SifirDoner()
    {
        var sut = CreateSut();

        sut.ToBase(0m, "USD").Should().Be(0m);
    }

    [Fact]
    public void ToBase_NegatifTutar_IsaretKorunarakDonusturulur()
    {
        // Negatif tutar RFM akışında normalde görülmez (harcamalar pozitif), ama sınıf bunu
        // reddetmiyor — bir iade/ters kayıt senaryosunda işaretin bozulmaması önemli.
        var sut = CreateSut();

        sut.ToBase(-100m, "USD").Should().Be(-3_500m);
    }

    [Fact]
    public void Constructor_TemelParaBirimi_BuyukHarfeCevrilipTrimlenir()
    {
        var sut = new CurrencyConverter("  try  ", new Dictionary<string, decimal>());

        sut.BaseCurrency.Should().Be("TRY");
    }

    [Fact]
    public void Constructor_BosTemelParaBirimi_ArgumentExceptionFirlatir()
    {
        var act = () => new CurrencyConverter("   ", new Dictionary<string, decimal>());

        act.Should().Throw<ArgumentException>();
    }
}
