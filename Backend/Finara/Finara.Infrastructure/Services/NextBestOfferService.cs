using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class NextBestOfferService(FinaraDbContext dbContext, IAuditLogRepository auditLogRepository) : INextBestOfferService
{
    public async Task<GenerateOffersResult?> GenerateOffersAsync(Guid customerId, int maxOffers, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
            return null;

        var segment = await dbContext.Segments
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);
        if (segment is null)
            throw new InvalidOperationException("Müşterinin aktif bir segmenti yok — önce POST /api/segmentation/recalculate çalıştırılmalı.");

        var referenceUtc = DateTime.UtcNow;
        var activeCampaigns = await dbContext.Campaigns
            // Süresi dolmuş (EndDateUtc geçmişte) bir kampanya Status=Active kalmış olsa bile
            // önerilmemeli — Faz 4 denetiminde bulundu: aktive edilmiş ama bitiş tarihi geçmiş
            // bir kampanya müşteriye öneriliyordu.
            .Where(c => c.Status == CampaignStatus.Active && (c.EndDateUtc == null || c.EndDateUtc > referenceUtc))
            .ToListAsync(cancellationToken);

        // Aynı kampanya için zaten bekleyen bir teklif varsa tekrar üretme —
        // arka arkaya çağrılırsa aynı teklif çoğaltılmasın diye.
        var campaignsWithPendingOffer = await dbContext.CampaignOffers
            .Where(o => o.CustomerId == customerId && o.Response == OfferResponse.Pending)
            .Select(o => o.CampaignId)
            .ToListAsync(cancellationToken);

        var scoredCampaigns = activeCampaigns
            .Where(c => !campaignsWithPendingOffer.Contains(c.Id))
            .Select(c => (Campaign: c, Score: NextBestOfferScorer.Score(segment, customer, c)))
            .Where(x => NextBestOfferScorer.MeetsThreshold(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(maxOffers)
            .ToList();

        foreach (var (campaign, score) in scoredCampaigns)
        {
            dbContext.CampaignOffers.Add(CampaignOffer.Create(campaign.Id, customerId, score));
            await auditLogRepository.InsertAsync(
                AuditLog.Create(customerId, AuditActionType.CampaignOfferPresented, $"{campaign.Name} (güven skoru: {score})"),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GenerateOffersResult(scoredCampaigns.Count);
    }

    public async Task<IReadOnlyList<OfferSummary>?> GetOffersAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerExists = await dbContext.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!customerExists)
            return null;

        var offers = await dbContext.CampaignOffers
            .AsNoTracking()
            .Include(o => o.Campaign)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PresentedAtUtc)
            .ToListAsync(cancellationToken);

        return offers
            .Select(o => new OfferSummary(o.Id, o.CampaignId, o.Campaign?.Name ?? string.Empty, o.CustomerId, o.ConfidenceScore, o.Response.ToString(), o.PresentedAtUtc))
            .ToList();
    }

    public Task<OfferSummary?> AcceptOfferAsync(Guid customerId, Guid offerId, CancellationToken cancellationToken = default) =>
        RespondToOfferAsync(customerId, offerId, offer => offer.Accept(), AuditActionType.CampaignOfferAccepted, cancellationToken);

    public Task<OfferSummary?> DeclineOfferAsync(Guid customerId, Guid offerId, CancellationToken cancellationToken = default) =>
        RespondToOfferAsync(customerId, offerId, offer => offer.Decline(), AuditActionType.CampaignOfferDeclined, cancellationToken);

    private async Task<OfferSummary?> RespondToOfferAsync(Guid customerId, Guid offerId, Action<CampaignOffer> respond, AuditActionType actionType, CancellationToken cancellationToken)
    {
        var offer = await dbContext.CampaignOffers
            .Include(o => o.Campaign)
            .FirstOrDefaultAsync(o => o.Id == offerId && o.CustomerId == customerId, cancellationToken);
        if (offer is null)
            return null;

        respond(offer);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(customerId, actionType, offer.Campaign?.Name ?? string.Empty),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OfferSummary(offer.Id, offer.CampaignId, offer.Campaign?.Name ?? string.Empty, offer.CustomerId, offer.ConfidenceScore, offer.Response.ToString(), offer.PresentedAtUtc);
    }
}
