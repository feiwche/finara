using Finara.Domain.Common;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Domain.Enums;
using Finara.Infrastructure.Persistence;

namespace Finara.Infrastructure.Services;

// Gerçek bir SMS sağlayıcısı yok — DispatchAsync sadece rıza kontrolünden geçirip
// NotificationLog'a yazıyor (mock "gönderim"). IConsentGate constructor'dan alınıyor:
// bu, rıza kontrolünün ÇAĞIRAN koddan bağımsız, dispatcher'ın kendisinde zorunlu
// olmasını sağlıyor (bkz. INotificationDispatcher XML yorumu).
public class MockSmsDispatcher(FinaraDbContext dbContext, IConsentGate consentGate) : INotificationDispatcher
{
    public ConsentChannel Channel => ConsentChannel.Sms;

    public async Task<NotificationDispatchResult> DispatchAsync(
        Guid bankId, Guid customerId, string message, Guid? campaignOfferId, CancellationToken cancellationToken = default)
    {
        var granted = await consentGate.IsConsentGrantedAsync(customerId, Channel, cancellationToken);
        var result = granted
            ? new NotificationDispatchResult(true, null)
            : new NotificationDispatchResult(false, NotificationSkipReasons.ConsentNotGranted);

        dbContext.NotificationLogs.Add(NotificationLog.Create(
            bankId, customerId, Channel, message, result.Sent, result.SkippedReason, campaignOfferId));
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
