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
            targetPropensities: [SpendingPropensity.HighlyResponsive]);

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

    [Fact]
    public void SurekliReddedilenKampanya_YeterliOrneklemVarsaDusukPuanAlir()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(4, 3, 5, SpendingPropensity.HighlyResponsive, TransactionCategory.Motorcycle);
        var kaskoKampanyasi = Campaign.Create(ValidBankId, "Motosiklet Kaskosu", "Açıklama", DateTime.UtcNow, targetCategory: TransactionCategory.Motorcycle);

        // Ayse_MotosikletMeraklisi testindeki AYNI senaryo (ceza olmadan 86 puan alıyordu) —
        // ama bu kampanya şimdiye kadar 10 kez yanıtlanmış ve 8'i reddedilmiş (%80 ret oranı).
        var scoreWithoutHistory = NextBestOfferScorer.Score(segment, customer, kaskoKampanyasi);
        var scoreWithBadHistory = NextBestOfferScorer.Score(segment, customer, kaskoKampanyasi, respondedCount: 10, declinedCount: 8);

        scoreWithoutHistory.Should().Be(86);
        // %80 ret oranı * 15 maksimum ceza = 12 puan düşüş → 86 - 12 = 74.
        scoreWithBadHistory.Should().Be(74);
    }

    [Fact]
    public void AzSayidaYanitlanmisKampanya_CezaAlmaz_OrneklemKucukOlduguIcin()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(4, 3, 5, SpendingPropensity.HighlyResponsive, TransactionCategory.Motorcycle);
        var kaskoKampanyasi = Campaign.Create(ValidBankId, "Motosiklet Kaskosu", "Açıklama", DateTime.UtcNow, targetCategory: TransactionCategory.Motorcycle);

        // Sadece 2 yanıt (MinRespondedSampleSize=5'in altında), ikisi de ret — yine de erken
        // gürültülü bir örneklemle yeni bir kampanyayı haksız cezalandırmamak için ceza YOK.
        var score = NextBestOfferScorer.Score(segment, customer, kaskoKampanyasi, respondedCount: 2, declinedCount: 2);

        score.Should().Be(86);
    }

    [Fact]
    public void TamamenKabulEdilenKampanya_CezaAlmaz()
    {
        var customer = CreateCustomer();
        var segment = CreateSegment(4, 3, 5, SpendingPropensity.HighlyResponsive, TransactionCategory.Motorcycle);
        var kaskoKampanyasi = Campaign.Create(ValidBankId, "Motosiklet Kaskosu", "Açıklama", DateTime.UtcNow, targetCategory: TransactionCategory.Motorcycle);

        var score = NextBestOfferScorer.Score(segment, customer, kaskoKampanyasi, respondedCount: 10, declinedCount: 0);

        score.Should().Be(86);
    }
}
