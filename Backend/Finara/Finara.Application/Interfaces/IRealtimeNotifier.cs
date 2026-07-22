using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

// Application/Infrastructure katmanları SignalR'a doğrudan bağımlı olmasın diye
// (Infrastructure "plain" SDK projesi) — gerçek implementasyon Finara.Api'de
// (IHubContext<NotificationsHub> gerektiriyor), ICurrentUserContext ile aynı desen.
public interface IRealtimeNotifier
{
    Task NotifyCampaignUpdatedAsync(Guid bankId, CampaignSummary campaign, CancellationToken cancellationToken = default);

    Task NotifyOfferDistributedAsync(Guid bankId, NotificationLogSummary log, CancellationToken cancellationToken = default);
}
