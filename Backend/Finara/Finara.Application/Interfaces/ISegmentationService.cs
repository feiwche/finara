using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ISegmentationService
{
    Task<RecalculateSegmentsResult> RecalculateAllAsync(CancellationToken cancellationToken = default);

    Task<SegmentSummary?> GetActiveSegmentAsync(Guid customerId, CancellationToken cancellationToken = default);

    // Aktif olanla birlikte deaktive edilmiş tüm geçmiş segment kayıtlarını CalculatedAtUtc'ye
    // göre artan sırada döner — segment geçmişi grafiği için. Müşteri yoksa/başka bankadaysa null
    // (404), müşteri var ama hiç segment hesaplanmamışsa boş liste (200) döner.
    Task<IReadOnlyList<SegmentSummary>?> GetSegmentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
