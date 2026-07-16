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
        user.Role.Should().Be(UserRole.Admin);
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
}
