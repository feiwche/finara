using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface INextBestOfferService
{
    // customerId bulunamazsa null döner (API katmanı 404'e çevirir); müşterinin aktif
    // segmenti yoksa InvalidOperationException fırlatır (önce segmentasyon çalıştırılmalı).
    Task<GenerateOffersResult?> GenerateOffersAsync(Guid customerId, int maxOffers, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferSummary>?> GetOffersAsync(Guid customerId, CancellationToken cancellationToken = default);
}
