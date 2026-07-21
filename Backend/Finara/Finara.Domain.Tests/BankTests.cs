using AwesomeAssertions;
using Xunit;

namespace Finara.Domain.Tests;

public class BankTests
{
    [Fact]
    public void Create_GecerliBilgilerle_BankaOlusturur()
    {
        var bank = Domain.Entities.Bank.Create("Test Bankası", "test");

        bank.Name.Should().Be("Test Bankası");
        bank.Code.Should().Be("TEST");
        bank.IsActive.Should().BeTrue();
        bank.DataProviderType.Should().Be(Domain.Enums.BankDataProviderType.Mock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosAd_ArgumentExceptionFirlatir(string name)
    {
        var act = () => Domain.Entities.Bank.Create(name, "TEST");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosKod_ArgumentExceptionFirlatir(string code)
    {
        var act = () => Domain.Entities.Bank.Create("Test Bankası", code);

        act.Should().Throw<ArgumentException>().WithParameterName("code");
    }

    [Fact]
    public void Deactivate_AktifBankayiPasiflestirir()
    {
        var bank = Domain.Entities.Bank.Create("Test Bankası", "TEST");

        bank.Deactivate();

        bank.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ZatenPasifBankada_InvalidOperationFirlatir()
    {
        var bank = Domain.Entities.Bank.Create("Test Bankası", "TEST");
        bank.Deactivate();

        var act = () => bank.Deactivate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetDataProviderType_ProviderTipiniGunceller()
    {
        var bank = Domain.Entities.Bank.Create("Test Bankası", "TEST");

        bank.SetDataProviderType(Domain.Enums.BankDataProviderType.GarantiSandbox);

        bank.DataProviderType.Should().Be(Domain.Enums.BankDataProviderType.GarantiSandbox);
    }
}
