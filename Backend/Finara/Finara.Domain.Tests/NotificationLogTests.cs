using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class NotificationLogTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_Gonderildi_SkippedReasonNullOlur()
    {
        var log = Domain.Entities.NotificationLog.Create(
            ValidBankId, ValidCustomerId, ConsentChannel.Sms, "Kaskonuzda %20 indirim!", sent: true);

        log.Sent.Should().BeTrue();
        log.SkippedReason.Should().BeNull();
    }

    [Fact]
    public void Create_Atlandi_SkippedReasonZorunlu()
    {
        var act = () => Domain.Entities.NotificationLog.Create(
            ValidBankId, ValidCustomerId, ConsentChannel.Sms, "Kaskonuzda %20 indirim!", sent: false);

        act.Should().Throw<ArgumentException>().WithParameterName("skippedReason");
    }

    [Fact]
    public void Create_AtlandiNedenBelirtilerek_Olusturur()
    {
        var log = Domain.Entities.NotificationLog.Create(
            ValidBankId, ValidCustomerId, ConsentChannel.Email, "Kaskonuzda %20 indirim!", sent: false, skippedReason: "Rıza verilmemiş");

        log.Sent.Should().BeFalse();
        log.SkippedReason.Should().Be("Rıza verilmemiş");
    }

    [Fact]
    public void Create_BosMesaj_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.NotificationLog.Create(
            ValidBankId, ValidCustomerId, ConsentChannel.Sms, "", sent: true);

        act.Should().Throw<ArgumentException>().WithParameterName("message");
    }
}
