using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Finara.Api.Realtime;

// Her olay hem kendi bankasının grubuna hem "superadmins" grubuna yayınlanıyor —
// SuperAdmin tüm bankaların canlı akışını görür (Dashboard/Kohort gibi diğer
// SuperAdmin-tüm-bankalar davranışlarıyla tutarlı).
public class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyCampaignUpdatedAsync(Guid bankId, CampaignSummary campaign, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Groups($"bank-{bankId}", "superadmins")
            .SendAsync("campaignUpdated", campaign, cancellationToken);

    public Task NotifyOfferDistributedAsync(Guid bankId, NotificationLogSummary log, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Groups($"bank-{bankId}", "superadmins")
            .SendAsync("offerDistributed", log, cancellationToken);
}
