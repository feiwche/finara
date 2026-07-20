using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Create_GecerliBilgilerle_KullaniciOlusturur()
    {
        var user = Domain.Entities.User.Create("Admin@Example.com", "hashed-value");

        user.Email.Should().Be("admin@example.com");
        user.PasswordHash.Should().Be("hashed-value");
        user.Role.Should().Be(UserRole.SuperAdmin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosEmail_ArgumentExceptionFirlatir(string email)
    {
        var act = () => Domain.Entities.User.Create(email, "hashed-value");

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("gecersiz-email-formati")]
    [InlineData("gecersiz@")]
    [InlineData("@example.com")]
    public void Create_GecersizEmailFormati_ArgumentExceptionFirlatir(string email)
    {
        var act = () => Domain.Entities.User.Create(email, "hashed-value");

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BosSifreHash_ArgumentExceptionFirlatir(string hash)
    {
        var act = () => Domain.Entities.User.Create("admin@example.com", hash);

        act.Should().Throw<ArgumentException>().WithParameterName("passwordHash");
    }

    [Fact]
    public void Create_VarsayilanOlarakAktifVeBankIdSiz()
    {
        var user = Domain.Entities.User.Create("admin@example.com", "hashed-value");

        user.IsActive.Should().BeTrue();
        user.BankId.Should().BeNull();
    }

    [Fact]
    public void Create_BankAdminBankIdIleOlusturulabilir()
    {
        var bankId = Guid.NewGuid();

        var user = Domain.Entities.User.Create("admin@example.com", "hashed-value", UserRole.BankAdmin, bankId);

        user.BankId.Should().Be(bankId);
        user.Role.Should().Be(UserRole.BankAdmin);
    }

    [Fact]
    public void Create_BankAdminBankIdSiz_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.User.Create("admin@example.com", "hashed-value", UserRole.BankAdmin);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Create_SuperAdminBankIdIle_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.User.Create("admin@example.com", "hashed-value", UserRole.SuperAdmin, Guid.NewGuid());

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Deactivate_AktifKullaniciyiPasiflestirir()
    {
        var user = Domain.Entities.User.Create("admin@example.com", "hashed-value");

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ZatenPasifKullanicida_InvalidOperationFirlatir()
    {
        var user = Domain.Entities.User.Create("admin@example.com", "hashed-value");
        user.Deactivate();

        var act = () => user.Deactivate();

        act.Should().Throw<InvalidOperationException>();
    }
}
