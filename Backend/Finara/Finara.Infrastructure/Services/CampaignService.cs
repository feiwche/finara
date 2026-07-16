using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class CampaignService(FinaraDbContext dbContext) : ICampaignService
{
    public async Task<CampaignSummary> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = Campaign.Create(
            request.Name,
            request.Description,
            request.StartDateUtc,
            request.VariantId,
            request.TargetCategory,
            request.TargetPropensity,
            request.EndDateUtc,
            request.TargetsInvestmentAccountHolders,
            request.TargetsInternationalTransferUsers);

        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(campaign);
    }

    public async Task<CampaignSummary?> ActivateCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        if (campaign is null)
            return null;

        campaign.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSummary(campaign);
    }

    public async Task<IReadOnlyList<CampaignSummary>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await dbContext.Campaigns.AsNoTracking().ToListAsync(cancellationToken);
        return campaigns.Select(ToSummary).ToList();
    }

    private static CampaignSummary ToSummary(Campaign campaign) => new(
        campaign.Id, campaign.Name, campaign.Description, campaign.Status, campaign.VariantId,
        campaign.TargetCategory, campaign.TargetPropensity,
        campaign.TargetsInvestmentAccountHolders, campaign.TargetsInternationalTransferUsers,
        campaign.StartDateUtc, campaign.EndDateUtc);
}
