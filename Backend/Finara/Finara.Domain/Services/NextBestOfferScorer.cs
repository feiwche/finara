using Finara.Domain.Entities;

namespace Finara.Domain.Services;

// Kural bazlı Next-Best-Offer puanlayıcısı. Faz 5'te AI katmanı bu kuralların üzerine
// eklenecek (ya da bunları zenginleştirecek), şimdilik tamamen açıklanabilir/deterministik.
//
// Puan dağılımı (0-100):
//   Kategori eşleşmesi        : +45 (kampanya kategori hedefliyorsa ve örtüşüyorsa; hedefsizse +15)
//   Eğilim eşleşmesi           : +25 (kampanya bir/daha çok eğilim hedefliyorsa ve segment bunlardan
//                                      birine eşitse; hedef listesi boşsa +25 — herkese uygun sayılır)
//   RFM aktiflik bonusu        : +0..20 (segmentin toplam RFM skoruna orantılı)
//   Özel ürün uygunluğu        : +10 (yatırım hesabı/SWIFT kullanan müşteri + ilgili kampanya)
//   Ret geçmişi cezası         : -0..15 (bu kampanya daha önce YETERİNCE yanıtlanmışsa —
//                                         bkz. MinRespondedSampleSize — ret oranıyla orantılı düşüş)
public static class NextBestOfferScorer
{
    public const int MinimumConfidenceThreshold = 30;

    // Bir kampanyanın ret geçmişine bakıp ceza uygulamadan önce en az bu kadar yanıtlanmış
    // (kabul+ret) teklifi olması gerekiyor — 1-2 erken ret yüzünden yeni bir kampanyayı
    // haksız yere cezalandırmamak için (küçük örneklem = gürültülü oran).
    public const int MinRespondedSampleSize = 5;
    private const int MaxDeclineHistoryPenalty = 15;

    // respondedCount/declinedCount BİLEREK opsiyonel (varsayılan 0/0 = ceza yok) — bu, ML
    // DEĞİL, geçmiş kabul/red sonuçlarından basit ve açıklanabilir bir geri bildirim: sürekli
    // reddedilen bir kampanya artık aynı skoru almaya devam etmiyor. Nihai karar hâlâ bu kural
    // motorunda, bir kara kutu modelde değil (bkz. Güvenlik & Veri Gizliliği sayfasındaki
    // "kural motoru her zaman birincil karar mekanizması" ilkesi).
    public static int Score(Segment segment, Customer customer, Campaign campaign, int respondedCount = 0, int declinedCount = 0)
    {
        var score = 0;

        // Kategori uyuşmazlığı SERT BİR ENGEL DEĞİL — bilinçli ürün kararı (denetimde
        // sorgulandı, böyle bırakıldı): kategori tutmasa bile eğilim (25) + RFM (20) +
        // özel bayrak (10) ile eşik aşılabilir. Gerekçe: baskın harcama kategorisi tek
        // başına belirleyici değildir — yatırım hesabı olan, yüksek RFM'li bir müşteriye
        // yatırım kampanyası önermek, o ay en çok markete harcamış olsa da doğrudur.
        // Kategori en yüksek ağırlığı (45) almaya devam ediyor, yani eşleşenler her zaman
        // önde. Bu davranış NextBestOfferScorerTests'te kilitli.
        if (campaign.TargetCategory is not null)
        {
            if (campaign.TargetCategory == segment.DominantCategory)
                score += 45;
        }
        else
        {
            score += 15;
        }

        if (campaign.TargetPropensities.Count == 0 || campaign.TargetPropensities.Contains(segment.Propensity))
            score += 25;

        score += (int)Math.Round(segment.GetTotalScore() / 15.0 * 20);

        var specialFlagMatch = (campaign.TargetsInvestmentAccountHolders && customer.HasInvestmentAccount)
            || (campaign.TargetsInternationalTransferUsers && customer.HasInternationalTransfers);
        if (specialFlagMatch)
            score += 10;

        if (respondedCount >= MinRespondedSampleSize)
        {
            var declineRate = declinedCount / (double)respondedCount;
            score -= (int)Math.Round(declineRate * MaxDeclineHistoryPenalty);
        }

        return Math.Clamp(score, 0, 100);
    }

    public static bool MeetsThreshold(int score) => score >= MinimumConfidenceThreshold;
}
