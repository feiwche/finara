using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface IOfferDistributionService
{
    // Müşteri/teklif bu banka scope'unda değilse ya da bulunamazsa null döner (404 deseni).
    Task<NotificationDispatchResult?> DistributeOfferAsync(Guid customerId, Guid offerId, DistributeOfferRequest request, CancellationToken cancellationToken = default);
}
