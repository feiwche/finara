using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class CustomerConsentTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_RizaVerilerek_GrantedAtDoluRevokedAtBosOlur()
    {
        var consent = Domain.Entities.CustomerConsent.Create(ValidBankId, ValidCustomerId, ConsentChannel.Sms, isGranted: true);

        consent.IsGranted.Should().BeTrue();
        consent.GrantedAtUtc.Should().NotBeNull();
        consent.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_RizaVerilmeden_RevokedAtDoluGrantedAtBosOlur()
    {
        var consent = Domain.Entities.CustomerConsent.Create(ValidBankId, ValidCustomerId, ConsentChannel.Email, isGranted: false);

        consent.IsGranted.Should().BeFalse();
        consent.GrantedAtUtc.Should().BeNull();
        consent.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Create_BosBankaId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.CustomerConsent.Create(Guid.Empty, ValidCustomerId, ConsentChannel.Sms, true);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Grant_RizayiVerir()
    {
        var consent = Domain.Entities.CustomerConsent.Create(ValidBankId, ValidCustomerId, ConsentChannel.Sms, isGranted: false);

        consent.Grant();

        consent.IsGranted.Should().BeTrue();
        consent.GrantedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_RizayiKaldirir()
    {
        var consent = Domain.Entities.CustomerConsent.Create(ValidBankId, ValidCustomerId, ConsentChannel.Sms, isGranted: true);

        consent.Revoke();

        consent.IsGranted.Should().BeFalse();
        consent.RevokedAtUtc.Should().NotBeNull();
    }
}
