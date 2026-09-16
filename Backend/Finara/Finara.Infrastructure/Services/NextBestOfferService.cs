using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Common;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Domain.Services;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class NextBestOfferService(FinaraDbContext dbContext, IAuditLogRepository auditLogRepository, ICurrentUserContext currentUser) : INextBestOfferService
{
    // CampaignOffer.Expire() yazılmıştı ama hiçbir yerden çağrılmıyordu (denetimde
    // bulundu) — bir müşteri bir teklife hiç yanıt vermezse (accept/decline) o kampanya
    // bu müşteriye SONSUZA DEK bir daha önerilmiyordu, çünkü aşağıdaki "zaten bekleyen
    // teklif var" kontrolü Pending durumunu süresiz kabul ediyordu. 30 gün, kampanya
    // tekliflerinin makul bir "yanıtsız kaldıysa artık geçerliliğini yitirir" penceresi.
    private const int PendingOfferExpiryDays = 30;

    public async Task<GenerateOffersResult?> GenerateOffersAsync(Guid customerId, int maxOffers, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
            return null;

        var segment = await dbContext.Segments
            .FirstOrDefaultAsync(s => s.CustomerId == customerId && s.IsActive, cancellationToken);
        if (segment is null)
            throw new DomainException(ErrorCodes.OfferNoActiveSegment, "Müşterinin aktif bir segmenti yok — önce POST /api/segmentation/recalculate çalıştırılmalı.");

        var created = await GenerateOffersForCustomerAsync(customer, segment, maxOffers, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new GenerateOffersResult(created);
    }

    public async Task<GenerateOffersResult> GenerateOffersForAllEligibleCustomersAsync(int maxOffersPerCustomer, CancellationToken cancellationToken = default)
    {
        // Aktif segmenti olan (yani en az bir kez segmentasyona girmiş), silinmemiş/anonimleştirilmemiş
        // tüm müşteriler taranır. Bank-scope filtresi burada da geçerli — SuperAdmin (otomasyon
        // dahil) tüm bankaları, BankAdmin sadece kendi bankasını kapsar.
        var segmentsQuery = dbContext.Segments.Where(s => s.IsActive);
        if (!currentUser.IsSuperAdmin)
            segmentsQuery = segmentsQuery.Where(s => s.BankId == currentUser.BankId);

        var eligibleSegments = await segmentsQuery.ToListAsync(cancellationToken);
        if (eligibleSegments.Count == 0)
            return new GenerateOffersResult(0);

        var customerIds = eligibleSegments.Select(s => s.CustomerId).ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id) && !c.IsErased)
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var totalCreated = 0;
        foreach (var segment in eligibleSegments)
        {
            if (!customers.TryGetValue(segment.CustomerId, out var customer))
                continue;

            totalCreated += await GenerateOffersForCustomerAsync(customer, segment, maxOffersPerCustomer, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new GenerateOffersResult(totalCreated);
    }

    private async Task<int> GenerateOffersForCustomerAsync(Customer customer, Segment segment, int maxOffers, CancellationToken cancellationToken)
    {
        var referenceUtc = DateTime.UtcNow;
        var activeCampaigns = await dbContext.Campaigns
            // Süresi dolmuş (EndDateUtc geçmişte) bir kampanya Status=Active kalmış olsa bile
            // önerilmemeli — Faz 4 denetiminde bulundu: aktive edilmiş ama bitiş tarihi geçmiş
            // bir kampanya müşteriye öneriliyordu. Kampanya, müşterinin KENDİ bankasına ait
            // olmalı — bankaların kampanya katalogları birbirine karışmaz.
            .Where(c => c.BankId == customer.BankId && c.Status == CampaignStatus.Active && (c.EndDateUtc == null || c.EndDateUtc > referenceUtc))
            .ToListAsync(cancellationToken);

        // Aynı kampanya için zaten bekleyen bir teklif varsa tekrar üretme —
        // arka arkaya çağrılırsa aynı teklif çoğaltılmasın diye. Ama önce PendingOfferExpiryDays'i
        // aşmış bekleyen teklifler expire ediliyor ki müşteri hiç yanıt vermediği bir teklif
        // yüzünden kampanya sonsuza dek "zaten bekliyor" sayılıp bir daha önerilmesin.
        var pendingOffers = await dbContext.CampaignOffers
            .Where(o => o.CustomerId == customer.Id && o.Response == OfferResponse.Pending)
            .ToListAsync(cancellationToken);

        var pendingExpiryThreshold = referenceUtc.AddDays(-PendingOfferExpiryDays);
        foreach (var stale in pendingOffers.Where(o => o.PresentedAtUtc < pendingExpiryThreshold))
            stale.Expire();

        // pendingOffers üzerindeki Response artık yukarıdaki Expire() çağrılarını yansıtıyor
        // (bellekte, henüz SaveChanges çağrılmadı) — bu yüzden DB'ye tekrar sorulmuyor, aynı
        // liste bellekte filtreleniyor.
        var campaignsWithPendingOffer = pendingOffers
            .Where(o => o.Response == OfferResponse.Pending)
            .Select(o => o.CampaignId)
            .ToList();

        // Kampanyanın (bu müşteriye özel değil, TÜM müşteriler genelinde) geçmiş kabul/ret
        // geçmişi — NextBestOfferScorer'daki ret-geçmişi cezasına girdi. Sürekli reddedilen
        // bir kampanya artık aynı skoru almaya devam etmiyor (bkz. o dosyadaki gerekçe).
        // "Yanıt" YALNIZCA gerçek bir karardır: kabul ya da ret. Expired (müşteri hiç yanıt
        // vermeden süresi doldu) bilinçli olarak DIŞARIDA — önceden `Response != Pending`
        // filtresi Expired'ları da paydaya katıyordu ve bu ret oranını sulandırıyordu
        // (denetimde bulundu): 3 ret + 10 expired, gerçekte %100 ret olması gerekirken
        // %23 olarak hesaplanıyordu. Dahası, hiç yanıtlanmamış bir kampanya yalnızca expire
        // olmuş teklifleriyle MinRespondedSampleSize eşiğini geçip "temiz geçmişli" görünüyordu.
        var campaignIds = activeCampaigns.Select(c => c.Id).ToList();
        var responseHistory = await dbContext.CampaignOffers
            .Where(o => campaignIds.Contains(o.CampaignId)
                && (o.Response == OfferResponse.Accepted || o.Response == OfferResponse.Declined))
            .GroupBy(o => o.CampaignId)
            .Select(g => new { CampaignId = g.Key, Responded = g.Count(), Declined = g.Count(o => o.Response == OfferResponse.Declined) })
            .ToDictionaryAsync(x => x.CampaignId, x => (x.Responded, x.Declined), cancellationToken);

        var scoredCampaigns = activeCampaigns
            .Where(c => !campaignsWithPendingOffer.Contains(c.Id))
            .Select(c =>
            {
                var (responded, declined) = responseHistory.GetValueOrDefault(c.Id);
                return (Campaign: c, Score: NextBestOfferScorer.Score(segment, customer, c, responded, declined));
            })
            .Where(x => NextBestOfferScorer.MeetsThreshold(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(maxOffers)
            .ToList();

        foreach (var (campaign, score) in scoredCampaigns)
        {
            dbContext.CampaignOffers.Add(CampaignOffer.Create(customer.BankId, campaign.Id, customer.Id, score));

            // SegmentationService'teki aynı gerekçe: currentUser.UserId null ise bu çağrı
            // SystemCurrentUserContext (otomasyon) üzerinden geliyor demektir — [Otomasyon]
            // etiketi Denetim Kaydı ekranında manuel "Teklif Üret" ile otomatik üretimi
            // ayırt edilebilir kılıyor.
            var details = $"{campaign.Name} (güven skoru: {score})";
            if (currentUser.UserId is null)
                details = $"[Otomasyon] {details}";

            await auditLogRepository.InsertAsync(
                AuditLog.Create(customer.BankId, customer.Id, AuditActionType.CampaignOfferPresented, details, currentUser.UserId),
                cancellationToken);
        }

        return scoredCampaigns.Count;
    }

    public async Task<IReadOnlyList<OfferSummary>?> GetOffersAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null || (!currentUser.IsSuperAdmin && customer.BankId != currentUser.BankId))
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
        if (offer is null || (!currentUser.IsSuperAdmin && offer.BankId != currentUser.BankId))
            return null;

        respond(offer);

        await auditLogRepository.InsertAsync(
            AuditLog.Create(offer.BankId, customerId, actionType, offer.Campaign?.Name ?? string.Empty, currentUser.UserId),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OfferSummary(offer.Id, offer.CampaignId, offer.Campaign?.Name ?? string.Empty, offer.CustomerId, offer.ConfidenceScore, offer.Response.ToString(), offer.PresentedAtUtc);
    }
}
