using System.Reflection;
using AwesomeAssertions;
using Finara.Domain.Entities;
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

    // Denetimde bulunan bug: CampaignOffer.Expire() yazılmıştı ama hiçbir yerden
    // çağrılmıyordu — bekleyen bir teklif yanıtlanmadığı sürece kampanya o müşteriye
    // sonsuza dek "zaten bekliyor" sayılıp önerilemiyordu. Bu iki test hem eski (>30 gün)
    // bir teklifin expire edilip kampanyanın tekrar önerilebilir hale geldiğini, hem de
    // taze (<30 gün) bir teklifin hâlâ engellemeye devam ettiğini (regresyon koruması)
    // kanıtlıyor.
    [Fact]
    public async Task GenerateOffersAsync_EskiBekleyenTeklifVarsa_ExpireEdipYeniTeklifUretir()
    {
        var campaign = CreateActiveCampaign("Fatura Kampanyası", TransactionCategory.Utilities);
        var customer = CreateCustomerWithSegment(
            "test@example.com", TransactionCategory.Utilities, SpendingPropensity.HighlyResponsive,
            recency: 4, frequency: 3, monetary: 5);

        SeedPendingOfferPresentedDaysAgo(campaign, customer, daysAgo: 31);

        var result = await Sut.GenerateOffersAsync(customer.Id, maxOffers: 1);

        result!.OffersCreated.Should().Be(1);
        var offers = await Sut.GetOffersAsync(customer.Id);
        offers.Should().HaveCount(2);
        offers!.Should().ContainSingle(o => o.Response == nameof(OfferResponse.Expired));
        offers!.Should().ContainSingle(o => o.Response == nameof(OfferResponse.Pending));
    }

    [Fact]
    public async Task GenerateOffersAsync_TazeBekleyenTeklifVarsa_TekrarTeklifUretmez()
    {
        var campaign = CreateActiveCampaign("Fatura Kampanyası", TransactionCategory.Utilities);
        var customer = CreateCustomerWithSegment(
            "test@example.com", TransactionCategory.Utilities, SpendingPropensity.HighlyResponsive,
            recency: 4, frequency: 3, monetary: 5);

        SeedPendingOfferPresentedDaysAgo(campaign, customer, daysAgo: 5);

        var result = await Sut.GenerateOffersAsync(customer.Id, maxOffers: 1);

        result!.OffersCreated.Should().Be(0);
        var offers = await Sut.GetOffersAsync(customer.Id);
        offers.Should().ContainSingle(o => o.Response == nameof(OfferResponse.Pending));
    }

    // Diğer servislerde (CustomerService, CampaignService, SegmentationService) bulunan çok-
    // kiracılı izolasyon hatasının aynısına bu serviste de açık bir yüzey vardı — hiçbir test
    // ikinci bir banka kullanmıyordu (denetimde bulundu). GenerateOffersForCustomerAsync'in
    // kampanya sorgusu zaten `c.BankId == customer.BankId` filtresi içeriyor; bu test o
    // filtrenin GERÇEKTEN çalıştığını kilitliyor.
    [Fact]
    public async Task GenerateOffersAsync_DigerBankaninKampanyasi_MusteriyeAslaOnerilmez()
    {
        // Diğer bankaya ait kampanya, müşterinin profiliyle MÜKEMMEL eşleşiyor (aynı kategori,
        // hedef eğilim, yüksek RFM) — banka filtresi olmasaydı kesinlikle önerilirdi.
        CreateActiveCampaign("Diğer Bankanın Kampanyası", TransactionCategory.Motorcycle, bankId: OtherBankId);
        var customer = CreateCustomerWithSegment(
            "test@example.com", TransactionCategory.Motorcycle, SpendingPropensity.HighlyResponsive,
            recency: 5, frequency: 5, monetary: 5);

        var result = await Sut.GenerateOffersAsync(customer.Id, maxOffers: 5);

        result!.OffersCreated.Should().Be(0);
        var offers = await Sut.GetOffersAsync(customer.Id);
        offers.Should().BeEmpty();
    }

    // AuthServiceTests.CreateDemoUser'daki aynı desen — private-set bir alanı test amaçlı
    // geçmişe çekmek için reflection kullanılıyor (Create() bilinçli olarak sadece "şimdi"
    // sunulmuş teklif üretebiliyor, geçmiş tarihli teklif domain API'sinde yok).
    private void SeedPendingOfferPresentedDaysAgo(Campaign campaign, Customer customer, int daysAgo)
    {
        var offer = CampaignOffer.Create(BankId, campaign.Id, customer.Id, confidenceScore: 50);
        typeof(CampaignOffer).GetProperty(nameof(CampaignOffer.PresentedAtUtc), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(offer, DateTime.UtcNow.AddDays(-daysAgo));
        DbContext.CampaignOffers.Add(offer);
        DbContext.SaveChanges();
    }
}
