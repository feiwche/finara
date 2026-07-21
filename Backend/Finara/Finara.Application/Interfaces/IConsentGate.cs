using Finara.Domain.Enums;

namespace Finara.Application.Interfaces;

// Faz 8.4 — "zorunlu geçiş noktası": her INotificationDispatcher implementasyonu bunu
// constructor'dan alır ve DispatchAsync içinde İLK iş olarak kontrol eder, çağıran koda
// bırakılmıyor. Sadece okuma metodu var (Grant/Revoke burada YOK, IConsentService'te) —
// bir dispatcher'ın rıza durumunu değiştirebilmesi en az yetki ilkesini ihlal ederdi.
public interface IConsentGate
{
    Task<bool> IsConsentGrantedAsync(Guid customerId, ConsentChannel channel, CancellationToken cancellationToken = default);
}
