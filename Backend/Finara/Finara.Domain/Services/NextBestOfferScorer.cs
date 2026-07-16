using Finara.Domain.Entities;

namespace Finara.Domain.Services;

// Kural bazlı Next-Best-Offer puanlayıcısı. Faz 5'te AI katmanı bu kuralların üzerine
// eklenecek (ya da bunları zenginleştirecek), şimdilik tamamen açıklanabilir/deterministik.
//
// Puan dağılımı (0-100):
//   Kategori eşleşmesi        : +45 (kampanya kategori hedefliyorsa ve örtüşüyorsa; hedefsizse +15)
//   Eğilim eşleşmesi           : +25 (kampanya eğilim hedefliyorsa ve örtüşüyorsa; hedefsizse +25 — herkese uygun sayılır)
//   RFM aktiflik bonusu        : +0..20 (segmentin toplam RFM skoruna orantılı)
//   Özel ürün uygunluğu        : +10 (yatırım hesabı/SWIFT kullanan müşteri + ilgili kampanya)
public static class NextBestOfferScorer
{
    public const int MinimumConfidenceThreshold = 30;

    public static int Score(Segment segment, Customer customer, Campaign campaign)
    {
        var score = 0;

        if (campaign.TargetCategory is not null)
        {
            if (campaign.TargetCategory == segment.DominantCategory)
                score += 45;
        }
        else
        {
            score += 15;
        }

        if (campaign.TargetPropensity is not null)
        {
            if (campaign.TargetPropensity == segment.Propensity)
                score += 25;
        }
        else
        {
            score += 25;
        }

        score += (int)Math.Round(segment.GetTotalScore() / 15.0 * 20);

        var specialFlagMatch = (campaign.TargetsInvestmentAccountHolders && customer.HasInvestmentAccount)
            || (campaign.TargetsInternationalTransferUsers && customer.HasInternationalTransfers);
        if (specialFlagMatch)
            score += 10;

        return Math.Clamp(score, 0, 100);
    }

    public static bool MeetsThreshold(int score) => score >= MinimumConfidenceThreshold;
}
