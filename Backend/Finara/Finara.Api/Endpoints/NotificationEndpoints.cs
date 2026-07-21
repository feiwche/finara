using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Enums;

namespace Finara.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        notifications.MapGet("/", async (
            Guid? customerId,
            ConsentChannel? channel,
            DateTime? fromUtc,
            DateTime? toUtc,
            INotificationLogService service,
            CancellationToken ct) =>
        {
            var filter = new NotificationLogFilter(customerId, channel, fromUtc, toUtc);
            return Results.Ok(await service.GetLogsAsync(filter, ct));
        })
        .Produces<IReadOnlyList<NotificationLogSummary>>();
    }
}
