using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class CampaignService(FinaraDbContext dbContext, ICurrentUserContext currentUser, IRealtimeNotifier realtimeNotifier) : ICampaignService
{
    public async Task<CampaignSummary> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = Campaign.Create(
            currentUser.ResolveBankIdForWrite(),
            request.Name,
            request.Description,
            request.StartDateUtc,
            request.VariantId,
            request.TargetCategory,
            request.TargetPropensities,
            request.EndDateUtc,
            request.TargetsInvestmentAccountHolders,
            request.TargetsInternationalTransferUsers);

        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(campaign);
    }

    public Task<CampaignSummary?> ActivateCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, c => c.Activate(), cancellationToken);

    public Task<CampaignSummary?> PauseCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, c => c.Pause(), cancellationToken);

    public Task<CampaignSummary?> CompleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, c => c.Complete(), cancellationToken);

    public Task<CampaignSummary?> ArchiveCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        TransitionAsync(campaignId, c => c.Archive(), cancellationToken);

    // Dört yaşam döngüsü geçişi de aynı iskeleti paylaşıyor: kampanyayı bul (yoksa null →
    // endpoint 404'e çevirir), domain durum-makinesi metodunu çağır (geçersiz geçişte
    // InvalidOperationException fırlatır → GlobalExceptionHandler 400'e çevirir), kaydet.
    private async Task<CampaignSummary?> TransitionAsync(Guid campaignId, Action<Campaign> transition, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        // Başka bankanın kampanyasına erişim denemesi de "bulunamadı" — o kampanyanın
        // başka bir bankada var olduğunu sızdırmaz.
        if (campaign is null || (!currentUser.IsSuperAdmin && campaign.BankId != currentUser.BankId))
            return null;

        transition(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = ToSummary(campaign);
        // Gerçek zamanlı yayın — açık olan diğer BackOffice/client sekmeleri (aynı banka +
        // SuperAdmin'ler) listelerini elle yenilemeden bu geçişi anında görsün diye.
        await realtimeNotifier.NotifyCampaignUpdatedAsync(campaign.BankId, summary, cancellationToken);
        return summary;
    }

    public async Task<IReadOnlyList<CampaignSummary>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var query = dbContext.Campaigns.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(c => c.BankId == currentUser.BankId);

        var campaigns = await query.ToListAsync(cancellationToken);
        return campaigns.Select(ToSummary).ToList();
    }

    private static CampaignSummary ToSummary(Campaign campaign) => new(
        campaign.Id, campaign.Name, campaign.Description, campaign.Status, campaign.VariantId,
        campaign.TargetCategory, campaign.TargetPropensities,
        campaign.TargetsInvestmentAccountHolders, campaign.TargetsInternationalTransferUsers,
        campaign.StartDateUtc, campaign.EndDateUtc);
}
