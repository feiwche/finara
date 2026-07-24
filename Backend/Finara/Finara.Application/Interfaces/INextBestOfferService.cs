using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface INextBestOfferService
{
    // customerId bulunamazsa null döner (API katmanı 404'e çevirir); müşterinin aktif
    // segmenti yoksa InvalidOperationException fırlatır (önce segmentasyon çalıştırılmalı).
    Task<GenerateOffersResult?> GenerateOffersAsync(Guid customerId, int maxOffers, CancellationToken cancellationToken = default);

    // Zamanlanmış otomasyon için — aktif bir segmenti olan TÜM müşteriler üzerinde
    // GenerateOffersAsync ile aynı skor/eşik mantığını tek tek çalıştırır. Segmenti
    // olmayan müşteriler (henüz hiç segmentasyona girmemiş) sessizce atlanır.
    Task<GenerateOffersResult> GenerateOffersForAllEligibleCustomersAsync(int maxOffersPerCustomer, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferSummary>?> GetOffersAsync(Guid customerId, CancellationToken cancellationToken = default);

    // customerId/offerId eşleşen bir teklif bulunamazsa null döner (API katmanı 404'e çevirir);
    // teklif zaten yanıtlanmışsa CampaignOffer.Accept()/Decline() InvalidOperationException fırlatır
    // (GlobalExceptionHandler otomatik 400'e çevirir).
    Task<OfferSummary?> AcceptOfferAsync(Guid customerId, Guid offerId, CancellationToken cancellationToken = default);

    Task<OfferSummary?> DeclineOfferAsync(Guid customerId, Guid offerId, CancellationToken cancellationToken = default);
}
