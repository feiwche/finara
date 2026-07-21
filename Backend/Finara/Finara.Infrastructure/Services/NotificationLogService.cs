using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class NotificationLogService(FinaraDbContext dbContext, ICurrentUserContext currentUser) : INotificationLogService
{
    public async Task<IReadOnlyList<NotificationLogSummary>> GetLogsAsync(NotificationLogFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.NotificationLogs.AsNoTracking().AsQueryable();
        if (!currentUser.IsSuperAdmin)
            query = query.Where(n => n.BankId == currentUser.BankId);

        if (filter.CustomerId is not null)
            query = query.Where(n => n.CustomerId == filter.CustomerId);
        if (filter.Channel is not null)
            query = query.Where(n => n.Channel == filter.Channel);
        if (filter.FromUtc is not null)
            query = query.Where(n => n.SentAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null)
            query = query.Where(n => n.SentAtUtc <= filter.ToUtc);

        return await query
            .OrderByDescending(n => n.SentAtUtc)
            .Join(dbContext.Customers.AsNoTracking(), n => n.CustomerId, c => c.Id,
                (n, c) => new NotificationLogSummary(
                    n.Id, n.CustomerId, c.FirstName + " " + c.LastName, n.CampaignOfferId,
                    n.Channel, n.Message, n.Sent, n.SkippedReason, n.SentAtUtc))
            .ToListAsync(cancellationToken);
    }
}
