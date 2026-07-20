using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface ICampaignService
{
    Task<CampaignSummary> CreateCampaignAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default);

    Task<CampaignSummary?> ActivateCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<CampaignSummary?> PauseCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<CampaignSummary?> CompleteCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<CampaignSummary?> ArchiveCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignSummary>> GetCampaignsAsync(CancellationToken cancellationToken = default);
}
