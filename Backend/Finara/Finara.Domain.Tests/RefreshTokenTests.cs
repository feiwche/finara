using AwesomeAssertions;
using Xunit;

namespace Finara.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliBilgilerle_TokenOlusturur()
    {
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var token = Domain.Entities.RefreshToken.Create(ValidUserId, "opaque-token-value", expiresAt);

        token.UserId.Should().Be(ValidUserId);
        token.Token.Should().Be("opaque-token-value");
        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_BosKullaniciId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.RefreshToken.Create(Guid.Empty, "token", DateTime.UtcNow.AddDays(7));

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void IsActive_SuresiDolmusTokenIcinFalseDoner()
    {
        var token = Domain.Entities.RefreshToken.Create(ValidUserId, "token", DateTime.UtcNow.AddSeconds(-1));

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_TokeniPasifHaleGetirir()
    {
        var token = Domain.Entities.RefreshToken.Create(ValidUserId, "token", DateTime.UtcNow.AddDays(7));

        token.Revoke();

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ZatenIptalEdilmisTokende_InvalidOperationFirlatir()
    {
        var token = Domain.Entities.RefreshToken.Create(ValidUserId, "token", DateTime.UtcNow.AddDays(7));
        token.Revoke();

        var act = () => token.Revoke();

        act.Should().Throw<InvalidOperationException>();
    }
}
