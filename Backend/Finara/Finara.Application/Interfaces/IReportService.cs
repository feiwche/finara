using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

// Dashboard'ın anlık KPI'larını (bkz. Faz 7.3) tamamlayan zaman-bazlı trendler —
// var olan AuditLogs verisinden (CustomerCreated/CampaignOfferAccepted/Declined)
// hesaplanıyor, yeni bir veri modeli gerekmiyor.
public interface IReportService
{
    Task<IReadOnlyList<DailyOfferTrendPoint>> GetOfferTrendAsync(int days, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyCustomerGrowthPoint>> GetCustomerGrowthAsync(int days, CancellationToken cancellationToken = default);
}
