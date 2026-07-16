using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IAiInsightService
{
    // customerId bulunamazsa null döner (API katmanı 404'e çevirir).
    Task<AiInsightResult?> GenerateInsightAsync(Guid customerId, string? question, CancellationToken cancellationToken = default);
}
