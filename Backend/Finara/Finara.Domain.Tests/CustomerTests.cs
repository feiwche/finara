using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class CustomerTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();
    private static DateTime ValidBirthDate => DateTime.UtcNow.AddYears(-25);

    [Fact]
    public void Create_GecerliBilgilerle_MusteriOlusturur()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ayşe", "Yılmaz", "ayse@example.com", ValidBirthDate);

        customer.FirstName.Should().Be("Ayşe");
        customer.LastName.Should().Be("Yılmaz");
        customer.Email.Should().Be("ayse@example.com");
        customer.CurrentPropensity.Should().Be(SpendingPropensity.Neutral);
        customer.Transactions.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmailKucukHarfeCevrilir()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ALI@EXAMPLE.COM", ValidBirthDate);

        customer.Email.Should().Be("ali@example.com");
    }

    [Fact]
    public void Create_BosBankaId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Customer.Create(Guid.Empty, "Ali", "Veli", "ali@example.com", ValidBirthDate);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosAd_ArgumentExceptionFirlatir(string firstName)
    {
        var act = () => Domain.Entities.Customer.Create(ValidBankId, firstName, "Yılmaz", "test@example.com", ValidBirthDate);

        act.Should().Throw<ArgumentException>().WithParameterName("firstName");
    }

    [Theory]
    [InlineData("gecersiz-email")]
    [InlineData("gecersiz@")]
    [InlineData("")]
    public void Create_GecersizEmail_ArgumentExceptionFirlatir(string email)
    {
        var act = () => Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", email, ValidBirthDate);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void Create_18YasindanKucukMusteri_ArgumentExceptionFirlatir()
    {
        var underageBirthDate = DateTime.UtcNow.AddYears(-17);

        var act = () => Domain.Entities.Customer.Create(ValidBankId, "Genç", "Müşteri", "genc@example.com", underageBirthDate);

        act.Should().Throw<ArgumentException>().WithParameterName("dateOfBirth");
    }

    [Fact]
    public void GetAge_DogumTarihineGoreDogruYasiHesaplar()
    {
        var birthDate = DateTime.UtcNow.AddYears(-30).AddDays(1);
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Test", "Kullanıcı", "test@example.com", birthDate);

        customer.GetAge().Should().Be(29);
    }

    [Fact]
    public void UpdateFinancialFlags_BayraklariGunceller()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);

        customer.UpdateFinancialFlags(hasInvestmentAccount: true, hasInternationalTransfers: true);

        customer.HasInvestmentAccount.Should().BeTrue();
        customer.HasInternationalTransfers.Should().BeTrue();
    }

    [Fact]
    public void AssignPropensity_EgilimiGunceller()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);

        customer.AssignPropensity(SpendingPropensity.HighlyResponsive);

        customer.CurrentPropensity.Should().Be(SpendingPropensity.HighlyResponsive);
    }

    [Fact]
    public void UpdateEducationLevel_VarsayilanOlarakNull_SonraGuncellenebilir()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);
        customer.EducationLevel.Should().BeNull();

        customer.UpdateEducationLevel(EducationLevel.Lisans);

        customer.EducationLevel.Should().Be(EducationLevel.Lisans);
    }

    [Theory]
    [InlineData(20, AgeBracket.Age18To25)]
    [InlineData(30, AgeBracket.Age26To35)]
    [InlineData(40, AgeBracket.Age36To45)]
    [InlineData(50, AgeBracket.Age46To55)]
    [InlineData(60, AgeBracket.Age56Plus)]
    public void GetAgeBracket_YasaGoreDogruAraligiDoner(int age, AgeBracket expected)
    {
        var birthDate = DateTime.UtcNow.AddYears(-age).AddDays(-1);
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Test", "Kullanıcı", "test@example.com", birthDate);

        customer.GetAgeBracket().Should().Be(expected);
    }

    [Fact]
    public void UpdatePhoneNumber_VarsayilanOlarakNull_GecerliE164IleGuncellenebilir()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);
        customer.PhoneNumber.Should().BeNull();

        customer.UpdatePhoneNumber("+905551234567");

        customer.PhoneNumber.Should().Be("+905551234567");
    }

    [Fact]
    public void UpdatePhoneNumber_BosVerilirse_NullOlur()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);
        customer.UpdatePhoneNumber("+905551234567");

        customer.UpdatePhoneNumber(null);

        customer.PhoneNumber.Should().BeNull();
    }

    [Theory]
    [InlineData("05551234567")]
    [InlineData("+90 555 123 45 67")]
    [InlineData("abc")]
    [InlineData("+0123456789")]
    public void UpdatePhoneNumber_GecersizFormat_ArgumentExceptionFirlatir(string phoneNumber)
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);

        var act = () => customer.UpdatePhoneNumber(phoneNumber);

        act.Should().Throw<ArgumentException>().WithParameterName("phoneNumber");
    }

    [Fact]
    public void Erase_KimlikBelirleyicileriAnonimlestirirVeIsaretler()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ayşe", "Yılmaz", "ayse@example.com", ValidBirthDate);
        customer.UpdatePhoneNumber("+905551234567");
        customer.UpdateEducationLevel(EducationLevel.Lisans);

        customer.Erase();

        customer.FirstName.Should().Be("Silinmiş");
        customer.LastName.Should().Be("Müşteri");
        customer.Email.Should().Contain("erased-").And.Contain("@erased.finara.internal");
        customer.PhoneNumber.Should().BeNull();
        customer.EducationLevel.Should().BeNull();
        customer.IsErased.Should().BeTrue();
        customer.ErasedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Erase_ZatenAnonimlestirilmisMusteride_InvalidOperationExceptionFirlatir()
    {
        var customer = Domain.Entities.Customer.Create(ValidBankId, "Ali", "Veli", "ali@example.com", ValidBirthDate);
        customer.Erase();

        var act = () => customer.Erase();

        act.Should().Throw<InvalidOperationException>();
    }
}
