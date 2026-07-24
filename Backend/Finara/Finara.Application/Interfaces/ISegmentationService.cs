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

    // RFM birincil karar mekanizması olarak kalır — bu sadece talep üzerine üretilen, kısa
    // bir insan-okunur yorum katmanı (bkz. Segment.AiCommentary XML yorumu). Müşteri yoksa/
    // başka bankadaysa veya hiç aktif segmenti yoksa null (404).
    Task<SegmentSummary?> GenerateAiCommentaryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
