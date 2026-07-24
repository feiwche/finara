using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Domain.Tests;

public class CampaignTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();

    private static Domain.Entities.Campaign CreateDraftCampaign() =>
        Domain.Entities.Campaign.Create(ValidBankId, "Yaz Kampanyası", "Açıklama", DateTime.UtcNow);

    [Fact]
    public void Create_GecerliBilgilerle_TaslakKampanyaOlusturur()
    {
        var campaign = CreateDraftCampaign();

        campaign.Status.Should().Be(CampaignStatus.Draft);
        campaign.VariantId.Should().Be("default");
        campaign.Offers.Should().BeEmpty();
        campaign.TargetsInvestmentAccountHolders.Should().BeFalse();
        campaign.TargetsInternationalTransferUsers.Should().BeFalse();
    }

    [Fact]
    public void Create_HedefBayraklariVerilebilir()
    {
        var campaign = Domain.Entities.Campaign.Create(
            ValidBankId, "Seyahat Sigortası", "Açıklama", DateTime.UtcNow,
            targetCategory: TransactionCategory.Travel,
            targetsInvestmentAccountHolders: true,
            targetsInternationalTransferUsers: true);

        campaign.TargetsInvestmentAccountHolders.Should().BeTrue();
        campaign.TargetsInternationalTransferUsers.Should().BeTrue();
    }

    [Fact]
    public void Create_CokluHedefEgilimiVerilebilir()
    {
        var campaign = Domain.Entities.Campaign.Create(
            ValidBankId, "Çoklu Eğilim Kampanyası", "Açıklama", DateTime.UtcNow,
            targetPropensities: [SpendingPropensity.HighlyResponsive, SpendingPropensity.Neutral, SpendingPropensity.HighlyResponsive]);

        campaign.TargetPropensities.Should().BeEquivalentTo([SpendingPropensity.HighlyResponsive, SpendingPropensity.Neutral]);
    }

    [Fact]
    public void Create_BosBankaId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Campaign.Create(Guid.Empty, "Kampanya", "Açıklama", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("bankId");
    }

    [Fact]
    public void Create_BosAd_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.Campaign.Create(ValidBankId, "", "Açıklama", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Create_BitisTarihiBaslangictanOnce_ArgumentExceptionFirlatir()
    {
        var start = DateTime.UtcNow;
        var act = () => Domain.Entities.Campaign.Create(ValidBankId, "Kampanya", "Açıklama", start, endDateUtc: start.AddDays(-1));

        act.Should().Throw<ArgumentException>().WithParameterName("endDateUtc");
    }

    [Fact]
    public void Activate_TaslaktanAktifeGecer()
    {
        var campaign = CreateDraftCampaign();

        campaign.Activate();

        campaign.Status.Should().Be(CampaignStatus.Active);
    }

    [Fact]
    public void Activate_BitisTarihiGecmisteKalanKampanyada_InvalidOperationFirlatir()
    {
        var campaign = Domain.Entities.Campaign.Create(
            ValidBankId, "Eski Kampanya", "Açıklama",
            startDateUtc: DateTime.UtcNow.AddYears(-1),
            endDateUtc: DateTime.UtcNow.AddDays(-1));

        var act = () => campaign.Activate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Activate_TamamlanmisKampanyada_InvalidOperationFirlatir()
    {
        var campaign = CreateDraftCampaign();
        campaign.Activate();
        campaign.Complete();

        var act = () => campaign.Activate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Pause_AktifKampanyayiDuraklatir()
    {
        var campaign = CreateDraftCampaign();
        campaign.Activate();

        campaign.Pause();

        campaign.Status.Should().Be(CampaignStatus.Paused);
    }

    [Fact]
    public void Pause_TaslakKampanyada_InvalidOperationFirlatir()
    {
        var campaign = CreateDraftCampaign();

        var act = () => campaign.Pause();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PausedKampanya_TekrarAktiveEdilebilir()
    {
        var campaign = CreateDraftCampaign();
        campaign.Activate();
        campaign.Pause();

        campaign.Activate();

        campaign.Status.Should().Be(CampaignStatus.Active);
    }

    [Fact]
    public void Complete_AktifKampanyayiTamamlar()
    {
        var campaign = CreateDraftCampaign();
        campaign.Activate();

        campaign.Complete();

        campaign.Status.Should().Be(CampaignStatus.Completed);
    }

    [Fact]
    public void Complete_TaslakKampanyada_InvalidOperationFirlatir()
    {
        var campaign = CreateDraftCampaign();

        var act = () => campaign.Complete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_HerDurumdanArsivlenebilir()
    {
        var campaign = CreateDraftCampaign();

        campaign.Archive();

        campaign.Status.Should().Be(CampaignStatus.Archived);
    }
}
