using AwesomeAssertions;
using Finara.Domain.Enums;
using Xunit;

namespace Finara.Infrastructure.Tests;

public class NextBestOfferServiceTests : NextBestOfferServiceTestBase
{
    // NextBestOfferScorerTests'teki domain-seviyesi testin (SurekliReddedilenKampanya...)
    // uçtan uca (gerçek DB sorgusu üzerinden) doğrulanmış hali — NextBestOfferService'in
    // ret geçmişini gerçekten SORGULAYIP scorer'a doğru ilettiğini kanıtlıyor.
    [Fact]
    public async Task GenerateOffersAsync_KampanyaGecmisteCokReddedilmisse_DusukGuvenSkoruUretir()
    {
        var campaign = CreateActiveCampaign("Motosiklet Kaskosu", TransactionCategory.Motorcycle);
        // 10 geçmiş yanıt, 8'i ret (%80) — diğer müşterilere ait, bu test müşterisiyle ilgisi yok.
        SeedPastResponses(campaign, acceptedCount: 2, declinedCount: 8);

        var customer = CreateCustomerWithSegment(
            "test@example.com", TransactionCategory.Motorcycle, SpendingPropensity.HighlyResponsive,
            recency: 4, frequency: 3, monetary: 5);

        var result = await Sut.GenerateOffersAsync(customer.Id, maxOffers: 1);

        result!.OffersCreated.Should().Be(1);
        var offers = await Sut.GetOffersAsync(customer.Id);
        // Geçmişsiz aynı senaryo 86 puan alıyordu (bkz. NextBestOfferScorerTests) — %80 ret
        // oranı * 15 maksimum ceza = 12 puan düşüş → 74.
        offers!.Single().ConfidenceScore.Should().Be(74);
    }

    [Fact]
    public async Task GenerateOffersAsync_KampanyaninYeterliGecmisiYoksa_CezaUygulanmaz()
    {
        var campaign = CreateActiveCampaign("Motosiklet Kaskosu", TransactionCategory.Motorcycle);
        // Sadece 2 geçmiş yanıt (MinRespondedSampleSize=5'in altında), ikisi de ret.
        SeedPastResponses(campaign, acceptedCount: 0, declinedCount: 2);

        var customer = CreateCustomerWithSegment(
            "test@example.com", TransactionCategory.Motorcycle, SpendingPropensity.HighlyResponsive,
            recency: 4, frequency: 3, monetary: 5);

        await Sut.GenerateOffersAsync(customer.Id, maxOffers: 1);

        var offers = await Sut.GetOffersAsync(customer.Id);
        offers!.Single().ConfidenceScore.Should().Be(86);
    }
}
