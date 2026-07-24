using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class UserInvitationTests
{
    private static readonly Guid InvitedByUserId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliBilgilerle_DavetOlusturur()
    {
        var expiresAtUtc = DateTime.UtcNow.AddHours(24);

        var invitation = Domain.Entities.UserInvitation.Create(
            "Admin@Example.com", UserRole.SuperAdmin, null, "token-hash", expiresAtUtc, InvitedByUserId);

        invitation.Email.Should().Be("admin@example.com");
        invitation.Role.Should().Be(UserRole.SuperAdmin);
        invitation.BankId.Should().BeNull();
        invitation.IsPending.Should().BeTrue();
    }

    [Fact]
    public void Create_BankAdminBankIdSiz_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.UserInvitation.Create(
            "admin@example.com", UserRole.BankAdmin, null, "token-hash", DateTime.UtcNow.AddHours(24), InvitedByUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Create_SuperAdminBankIdIle_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.UserInvitation.Create(
            "admin@example.com", UserRole.SuperAdmin, Guid.NewGuid(), "token-hash", DateTime.UtcNow.AddHours(24), InvitedByUserId);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void IsPending_SuresiGecmisDavette_FalseDoner()
    {
        var invitation = Domain.Entities.UserInvitation.Create(
            "admin@example.com", UserRole.SuperAdmin, null, "token-hash", DateTime.UtcNow.AddSeconds(-1), InvitedByUserId);

        invitation.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Revoke_BekleyenDavetiIptalEder()
    {
        var invitation = Domain.Entities.UserInvitation.Create(
            "admin@example.com", UserRole.SuperAdmin, null, "token-hash", DateTime.UtcNow.AddHours(24), InvitedByUserId);

        invitation.Revoke();

        invitation.RevokedAtUtc.Should().NotBeNull();
        invitation.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Revoke_ZatenIptalEdilmisDavette_InvalidOperationFirlatir()
    {
        var invitation = Domain.Entities.UserInvitation.Create(
            "admin@example.com", UserRole.SuperAdmin, null, "token-hash", DateTime.UtcNow.AddHours(24), InvitedByUserId);
        invitation.Revoke();

        var act = () => invitation.Revoke();

        act.Should().Throw<InvalidOperationException>();
    }
}
