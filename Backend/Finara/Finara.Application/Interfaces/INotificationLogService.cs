using Finara.Application.Contracts;

namespace Finara.Application.Interfaces;

public interface INotificationLogService
{
    Task<IReadOnlyList<NotificationLogSummary>> GetLogsAsync(NotificationLogFilter filter, CancellationToken cancellationToken = default);
}
