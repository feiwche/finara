using Finara.Domain.Enums;

namespace Finara.Domain.Services;

// Saf, DB'den bağımsız hesaplama — girdi olarak müşteri popülasyonunun harcama
// özetini alır, RFM skorlarını üretir. DB sorgusu SegmentationService'te (Infrastructure).
public static class RfmCalculator
{
    // Yalnızca en az bir işlemi olan müşteriler için çağrılmalı — LastTransactionUtc'nin
    // null olabileceği "hiç işlemi yok" durumu bilerek desteklenmiyor (segmentasyona
    // dahil edilecek müşteri zaten en az bir işlemi olan olarak filtrelenir).
    public record CustomerActivity(Guid CustomerId, DateTime LastTransactionUtc, int TransactionCount, decimal TotalSpend);

    public record RfmScore(Guid CustomerId, int RecencyScore, int FrequencyScore, int MonetaryScore)
    {
        public int TotalScore => RecencyScore + FrequencyScore + MonetaryScore;
    }

    public static IReadOnlyList<RfmScore> CalculateScores(IReadOnlyList<CustomerActivity> activities, DateTime referenceUtc)
    {
        if (activities.Count == 0)
            return Array.Empty<RfmScore>();

        // Recency: son işlemden bu yana geçen gün — az olan iyi, bu yüzden higherIsBetter: false.
        var recencyScores = ScoreByQuintile(
            activities.Select(a => (a.CustomerId, Value: (decimal)(referenceUtc - a.LastTransactionUtc).TotalDays)),
            higherIsBetter: false);

        var frequencyScores = ScoreByQuintile(
            activities.Select(a => (a.CustomerId, Value: (decimal)a.TransactionCount)),
            higherIsBetter: true);

        var monetaryScores = ScoreByQuintile(
            activities.Select(a => (a.CustomerId, Value: a.TotalSpend)),
            higherIsBetter: true);

        return activities
            .Select(a => new RfmScore(a.CustomerId, recencyScores[a.CustomerId], frequencyScores[a.CustomerId], monetaryScores[a.CustomerId]))
            .ToList();
    }

    public static SpendingPropensity DeterminePropensity(int totalScore) => totalScore switch
    {
        <= 6 => SpendingPropensity.NeedsEngagement,
        <= 11 => SpendingPropensity.Neutral,
        _ => SpendingPropensity.HighlyResponsive
    };

    // SQL'deki NTILE(5) ile aynı mantık: popülasyon değere göre sıralanır, 5 eşit
    // parçaya bölünür, her parçaya 1-5 arası bir skor verilir.
    //
    // BİLİNÇLİ SAPMA (denetimde tespit edildi, ürün kararıyla böyle bırakıldı): 5'ten AZ
    // müşterili popülasyonlarda `i * 5 / n` formülü gerçek NTILE(5) davranışından ayrılır —
    // n=2'de skorlar 1 ve 3 (2 hiç kullanılmaz), n=4'te tavan 5 değil 4 olur. Gerçek NTILE
    // bu durumda herkesi en düşük gruplara yığardı.
    //
    // Neden düzeltilmedi: 2-4 müşterilik bir popülasyonda quintile zaten istatistiksel olarak
    // anlamsız; hangi davranışın "doğru" olduğu tanım gereği tartışmalı. Mevcut formül en
    // azından müşterileri birbirinden AYIRT ediyor, ki segmentasyonun amacı bu. Gerçek
    // bankalarda popülasyon her zaman n>>5 olacağı için pratik etkisi yok — bu yalnızca
    // yeni/küçük bir pilot bankanın ilk günlerinde görülür.
    // Davranış RfmCalculatorTests'te kilitli: değişirse test kırılır, sessizce kaymaz.
    private static Dictionary<Guid, int> ScoreByQuintile(IEnumerable<(Guid CustomerId, decimal Value)> values, bool higherIsBetter)
    {
        var ordered = values.OrderBy(v => v.Value).ToList();
        var n = ordered.Count;
        var result = new Dictionary<Guid, int>();

        // Tek müşterilik bir popülasyonda sıralama anlamsız — nötr (3) skor verilir,
        // rastgele en iyi ya da en kötü skoru vermek yerine.
        if (n == 1)
        {
            result[ordered[0].CustomerId] = 3;
            return result;
        }

        for (var i = 0; i < n; i++)
        {
            var bucket = i * 5 / n; // 0..4
            var rawScore = bucket + 1; // 1..5, sıralamada en düşük değer 1 alır
            result[ordered[i].CustomerId] = higherIsBetter ? rawScore : 6 - rawScore;
        }

        return result;
    }
}
