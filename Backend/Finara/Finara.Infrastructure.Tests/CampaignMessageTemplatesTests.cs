using AwesomeAssertions;
using Finara.Domain.Enums;
using Finara.Infrastructure.Services;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class CampaignMessageTemplatesTests
{
    [Fact]
    public void OfferDistributionMessage_Turkish_TurkceMetinDoner()
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motosiklet Kaskosu", 82, UiLanguage.Turkish);

        message.Should().Contain("Motosiklet Kaskosu").And.Contain("teklifiniz").And.Contain("%82");
    }

    [Fact]
    public void OfferDistributionMessage_English_IngilizceMetinDoner()
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motorcycle Insurance", 82, UiLanguage.English);

        message.Should().Contain("Motorcycle Insurance").And.Contain("Confidence score").And.Contain("82%");
    }

    [Fact]
    public void OfferDistributionMessage_German_AlmancaMetinDoner()
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motorradversicherung", 82, UiLanguage.German);

        message.Should().Contain("Motorradversicherung").And.Contain("Vertrauenswert").And.Contain("%82");
    }
}
