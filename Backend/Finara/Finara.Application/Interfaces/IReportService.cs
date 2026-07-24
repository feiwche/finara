using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

// Dashboard'ın anlık KPI'larını (bkz. Faz 7.3) tamamlayan zaman-bazlı trendler —
// var olan AuditLogs verisinden (CustomerCreated/CampaignOfferAccepted/Declined)
// hesaplanıyor, yeni bir veri modeli gerekmiyor.
public interface IReportService
{
    Task<IReadOnlyList<DailyOfferTrendPoint>> GetOfferTrendAsync(int days, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyCustomerGrowthPoint>> GetCustomerGrowthAsync(int days, CancellationToken cancellationToken = default);

    Task<OfferImpactSummary> GetOfferImpactSummaryAsync(CancellationToken cancellationToken = default);

    // Diğer metodların aksine ICurrentUserContext'ten DEĞİL, doğrudan verilen bankId'den
    // hesaplanıyor — WeeklyDigestService bir HTTP isteği/oturumu olmadan, birden çok
    // bankanın istatistiğini sırayla hesaplaması gerektiği için (bkz. o dosyadaki yorum).
    Task<WeeklyDigestStats> GetWeeklyDigestStatsAsync(Guid bankId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
