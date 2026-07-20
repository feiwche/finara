using AwesomeAssertions;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Xunit;

namespace Finara.Domain.Tests;

// Bu testler, kullanıcıyla Faz 4 başlamadan önce üzerinde anlaşılan somut örnek
// senaryoları (Ayşe/Mehmet/Zeynep) birebir kodluyor — puanlama formülü değişirse
// bu testler de bilinçli olarak güncellenmeli.
public class NextBestOfferScorerTests
{
    private static readonly Guid ValidBankId = Guid.NewGuid();

    private static Customer CreateCustomer(bool hasInvestment = false, bool hasInternational = false)
    {
        var customer = Customer.Create(ValidBankId, "Test", "Müşteri", $"{Guid.NewGuid()}@example.com", DateTime.UtcNow.AddYears(-30));
        customer.UpdateFinancialFlags(hasInvestment, hasInternational);
        return customer;
    }

    private static Segment CreateSegment(int recency, int frequency, int monetary, SpendingPropensity propensity, TransactionCategory? dominantCategory) =>
        Segment.Create(ValidBankId, Guid.NewGuid(), recency, frequency, monetary, propensity, dominantCategory);

    [Fact]
    public void Ayse_MotosikletMeraklisi_KaskoKampanyasindanYuksekPuanAlir()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(4, 3, 5, SpendingPropensity.HighlyResponsive, TransactionCategory.Motorcycle); // toplam 12
        var kaskoKampanyasi = Campaign.Create(ValidBankId, "Motosiklet Kaskosu", "Açıklama", DateTime.UtcNow, targetCategory: TransactionCategory.Motorcycle);

        var score = NextBestOfferScorer.Score(segment, customer, kaskoKampanyasi);

        score.Should().Be(86);
        NextBestOfferScorer.MeetsThreshold(score).Should().BeTrue();
    }

    [Fact]
    public void Mehmet_YatirimciVeSwiftKullanicisi_SeyahatSigortasindanCokYuksekPuanAlir()
    {
        var customer = CreateCustomer(hasInvestment: true, hasInternational: true);
        var segment = CreateSegment(4, 4, 5, SpendingPropensity.Neutral, TransactionCategory.Travel); // toplam 13
        var seyahatSigortasi = Campaign.Create(ValidBankId, 
            "Seyahat Sigortası", "Açıklama", DateTime.UtcNow,
            targetCategory: TransactionCategory.Travel,
            targetsInternationalTransferUsers: true);

        var score = NextBestOfferScorer.Score(segment, customer, seyahatSigortasi);

        score.Should().Be(97);
        NextBestOfferScorer.MeetsThreshold(score).Should().BeTrue();
    }

    [Fact]
    public void Mehmet_DovizKampanyasindanOrtaDuzeyPuanAlir_KategoriUymuyorAmaSwiftUyuyor()
    {
        var customer = CreateCustomer(hasInvestment: true, hasInternational: true);
        var segment = CreateSegment(4, 4, 5, SpendingPropensity.Neutral, TransactionCategory.Travel); // toplam 13, baskın kategori Travel
        var dovizKampanyasi = Campaign.Create(ValidBankId, 
            "Döviz Avantajlı Kredi Kartı", "Açıklama", DateTime.UtcNow,
            targetCategory: TransactionCategory.ForeignExchange,
            targetsInternationalTransferUsers: true);

        var score = NextBestOfferScorer.Score(segment, customer, dovizKampanyasi);

        score.Should().Be(52);
        NextBestOfferScorer.MeetsThreshold(score).Should().BeTrue();
    }

    [Fact]
    public void Zeynep_DusukAktiviteliMusteri_AlakasizKampanyadanEsikAltiPuanAlirVeOnerilmez()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(2, 1, 2, SpendingPropensity.NeedsEngagement, TransactionCategory.Groceries); // toplam 5
        var eTicaretKampanyasi = Campaign.Create(ValidBankId, 
            "E-ticaret Puan Kampanyası", "Açıklama", DateTime.UtcNow,
            targetCategory: TransactionCategory.ECommerce,
            targetPropensity: SpendingPropensity.HighlyResponsive);

        var score = NextBestOfferScorer.Score(segment, customer, eTicaretKampanyasi);

        score.Should().Be(7);
        NextBestOfferScorer.MeetsThreshold(score).Should().BeFalse();
    }

    [Fact]
    public void HedefsizGenelKampanya_HerMusteriyeKismiPuanVerir()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(3, 3, 3, SpendingPropensity.Neutral, TransactionCategory.Groceries); // toplam 9
        var genelKampanya = Campaign.Create(ValidBankId, "Genel Hoşgeldin Kampanyası", "Açıklama", DateTime.UtcNow);

        var score = NextBestOfferScorer.Score(segment, customer, genelKampanya);

        // kategori hedefsiz +15, eğilim hedefsiz +25, RFM 9/15*20=12, özel bayrak yok
        score.Should().Be(52);
    }
}
