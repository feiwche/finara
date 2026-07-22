using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class AuditLogTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    [Fact]
    public void Create_GecerliBilgilerle_KayitOlusturur()
    {
        var log = Domain.Entities.AuditLog.Create(
            ValidBankId, ValidCustomerId, AuditActionType.CustomerCreated, "İlk kayıt");

        log.CustomerId.Should().Be(ValidCustomerId);
        log.ActionType.Should().Be(AuditActionType.CustomerCreated);
        log.Details.Should().Be("İlk kayıt");
        log.PerformedByUserId.Should().BeNull();
        log.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_BosBankaId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.AuditLog.Create(Guid.Empty, ValidCustomerId, AuditActionType.CustomerCreated);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Create_BosMusteriId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.AuditLog.Create(ValidBankId, Guid.Empty, AuditActionType.CustomerCreated);

        act.Should().Throw<ArgumentException>().WithParameterName("customerId");
    }

    [Fact]
    public void Create_MusteriIdSizOlaylarIcinNullKabulEder()
    {
        // Yönetici hesabı oluşturma/pasifleştirme gibi bir müşteriyle ilgisi olmayan
        // denetim olayları için CustomerId=null geçerli, Guid.Empty değil.
        var log = Domain.Entities.AuditLog.Create(ValidBankId, null, AuditActionType.AdminCreated, "yeni@finara.dev");

        log.CustomerId.Should().BeNull();
        log.ActionType.Should().Be(AuditActionType.AdminCreated);
    }

    [Fact]
    public void Create_PerformedByUserIdVerilebilir()
    {
        var userId = Guid.NewGuid();

        var log = Domain.Entities.AuditLog.Create(
            ValidBankId, ValidCustomerId, AuditActionType.CampaignOfferAccepted, performedByUserId: userId);

        log.PerformedByUserId.Should().Be(userId);
    }
}
