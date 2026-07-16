using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ISegmentationService
{
    Task<RecalculateSegmentsResult> RecalculateAllAsync(CancellationToken cancellationToken = default);

    Task<SegmentSummary?> GetActiveSegmentAsync(Guid customerId, CancellationToken cancellationToken = default);
}
