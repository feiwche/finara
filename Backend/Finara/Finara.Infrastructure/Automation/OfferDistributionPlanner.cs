using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Automation;

// Otomatik dağıtımda HANGİ tekliflerin gönderileceğine karar veren kural. FinaraAutomationService'in
// içinden ayrıldı çünkü bu kuralın bozulması doğrudan gerçek müşterilere ileti spam'i demek —
// ilk sürümde tam olarak bu oldu: dağıtılmamış tüm teklifler gönderiliyordu ve bir müşterinin
// 5 bekleyen teklifi olabildiği için tek turda aynı kişiye 5 kampanya × 2 kanal = 10 ileti
// gitti (canlı veride doğrulandı). Ayrı ve test edilebilir bir yerde durması bilinçli.
public static class OfferDistributionPlanner
{
    public record PlannedOffer(Guid Id, Guid CustomerId, double ConfidenceScore);

    public static async Task<IReadOnlyList<PlannedOffer>> SelectAsync(
        FinaraDbContext dbContext, AutomationOptions options, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var quietPeriodStart = now.AddHours(-Math.Max(0, options.MinHoursBetweenCustomerMessages));

        var candidates = await dbContext.CampaignOffers
            .Where(o => o.Response == OfferResponse.Pending
                // Kampanya HÂLÂ dağıtılabilir durumda olmalı. Teklif ÜRETİLİRKEN bu kontrol
                // yapılıyor ama teklif "bekliyor" durumunda beklerken kampanya duraklatılmış,
                // tamamlanmış, arşivlenmiş ya da bitiş tarihi geçmiş olabilir — bu durumlar
                // bekleyen teklifleri iptal etmiyor (bkz. Campaign.Pause/Complete/Archive).
                // Kontrol olmadan, durdurulmuş bir kampanyanın teklifi müşteriye gönderiliyordu
                // (denetimde bulundu).
                && dbContext.Campaigns.Any(c => c.Id == o.CampaignId
                    && c.Status == CampaignStatus.Active
                    && (c.EndDateUtc == null || c.EndDateUtc > now))
                // Bu teklif daha önce hiç dağıtılmamış olmalı. Rıza reddi de bir NotificationLog
                // satırı yazdığı için, rızası olmayan müşterinin aynı teklifi her turda yeniden
                // denenmez.
                && !dbContext.NotificationLogs.Any(n => n.CampaignOfferId == o.Id)
                // Müşteri yakın zamanda ileti almamış olmalı — otomasyon 30 dakikada bir
                // çalıştığı için bu olmadan aynı kişiye gün içinde onlarca ileti giderdi.
                && !dbContext.NotificationLogs.Any(n => n.CustomerId == o.CustomerId && n.SentAtUtc >= quietPeriodStart))
            .Select(o => new PlannedOffer(o.Id, o.CustomerId, o.ConfidenceScore))
            .ToListAsync(cancellationToken);

        // Müşteri başına TEK teklif, güven skoru en yüksek olan. Ürünün kendi "Next-Best-Offer"
        // mantığı zaten budur: en iyi teklifi sun, hepsini değil. Eşitlikte kimlik sırası —
        // seçimin belirli ve tekrar edilebilir olması için.
        return candidates
            .GroupBy(o => o.CustomerId)
            .Select(g => g.OrderByDescending(o => o.ConfidenceScore).ThenBy(o => o.Id).First())
            .OrderByDescending(o => o.ConfidenceScore)
            .Take(Math.Max(1, options.MaxDistributionsPerCycle))
            .ToList();
    }
}
