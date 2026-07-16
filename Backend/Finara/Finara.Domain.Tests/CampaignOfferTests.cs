using AwesomeAssertions;
using Xunit;

namespace Finara.Domain.Tests;

public class CampaignOfferTests
{
    private static readonly Guid ValidCampaignId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();

    private static Domain.Entities.CampaignOffer CreatePendingOffer(double confidence = 80) =>
        Domain.Entities.CampaignOffer.Create(ValidCampaignId, ValidCustomerId, confidence);

    [Fact]
    public void Create_GecerliBilgilerle_BekleyenTeklifOlusturur()
    {
        var offer = CreatePendingOffer();

        offer.Response.Should().Be(Domain.Entities.OfferResponse.Pending);
        offer.ConfidenceScore.Should().Be(80);
        offer.RespondedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_BosKampanyaId_ArgumentExceptionFirlatir()
    {
        var act = () => Domain.Entities.CampaignOffer.Create(Guid.Empty, ValidCustomerId, 50);

        act.Should().Throw<ArgumentException>().WithParameterName("campaignId");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_GecersizGuvenSkoru_ArgumentOutOfRangeFirlatir(double confidence)
    {
        var act = () => Domain.Entities.CampaignOffer.Create(ValidCampaignId, ValidCustomerId, confidence);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("confidenceScore");
    }

    [Fact]
    public void Accept_BekleyenTeklifiKabulEder()
    {
        var offer = CreatePendingOffer();

        offer.Accept();

        offer.Response.Should().Be(Domain.Entities.OfferResponse.Accepted);
        offer.RespondedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Decline_BekleyenTeklifiReddeder()
    {
        var offer = CreatePendingOffer();

        offer.Decline();

        offer.Response.Should().Be(Domain.Entities.OfferResponse.Declined);
    }

    [Fact]
    public void Accept_ZatenYanitlanmisTeklifte_InvalidOperationFirlatir()
    {
        var offer = CreatePendingOffer();
        offer.Accept();

        var act = () => offer.Accept();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Expire_BekleyenTeklifinSuresiniDoldurur()
    {
        var offer = CreatePendingOffer();

        offer.Expire();

        offer.Response.Should().Be(Domain.Entities.OfferResponse.Expired);
    }

    [Fact]
    public void Expire_ZatenKabulEdilmisTeklifte_SessizceYoksayar()
    {
        var offer = CreatePendingOffer();
        offer.Accept();

        offer.Expire();

        offer.Response.Should().Be(Domain.Entities.OfferResponse.Accepted);
    }
}
