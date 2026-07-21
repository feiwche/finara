using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IConsentService
{
    // Müşteri bu banka scope'unda değilse null döner (404 deseni).
    Task<IReadOnlyList<ConsentSummary>?> GetConsentsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<ConsentSummary?> UpdateConsentAsync(Guid customerId, UpdateConsentRequest request, CancellationToken cancellationToken = default);
}
