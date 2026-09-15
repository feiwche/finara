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
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motosiklet Kaskosu", UiLanguage.Turkish);

        message.Should().Contain("Motosiklet Kaskosu").And.Contain("teklifiniz");
    }

    [Fact]
    public void OfferDistributionMessage_English_IngilizceMetinDoner()
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motorcycle Insurance", UiLanguage.English);

        message.Should().Contain("Motorcycle Insurance").And.Contain("offer is waiting");
    }

    [Fact]
    public void OfferDistributionMessage_German_AlmancaMetinDoner()
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Motorradversicherung", UiLanguage.German);

        message.Should().Contain("Motorradversicherung").And.Contain("Angebot wartet");
    }

    // Güven skoru bankanın iç metriği — müşteriye giden mesajda hiçbir dilde yer almamalı
    // (denetimde bulundu: önceden "Güven skoru: %49" olarak gönderiliyordu). Eski testler
    // skoru metinde ARIYORDU, yani hatayı kilitlemişlerdi; bu test tam tersini garanti ediyor.
    [Theory]
    [InlineData(UiLanguage.Turkish)]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    public void OfferDistributionMessage_GuvenSkorunuMusteriyeSizdirmaz(UiLanguage language)
    {
        var message = CampaignMessageTemplates.OfferDistributionMessage("Test Kampanyası", language);

        message.Should().NotContain("%")
            .And.NotContainAny("skor", "Skor", "score", "Score", "Vertrauenswert");
    }
}
