using Finara.Application.Contracts;
using Finara.Domain.Enums;

namespace Finara.Application.Interfaces;

// Faz 8.4 — bir kanaldan bildirim "göndermeyi" soyutlar (bugün mock implementasyonlar,
// gerçek SMS/e-posta sağlayıcısı yok). Implementasyonlar IConsentGate'i constructor'dan
// alıp DispatchAsync'te İLK iş olarak kontrol etmek ZORUNDA — bkz. IConsentGate.
public interface INotificationDispatcher
{
    ConsentChannel Channel { get; }

    Task<NotificationDispatchResult> DispatchAsync(Guid bankId, Guid customerId, string message, Guid? campaignOfferId, CancellationToken cancellationToken = default);
}
