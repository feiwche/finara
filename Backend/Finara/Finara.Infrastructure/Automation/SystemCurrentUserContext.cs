using Finara.Application.Interfaces;
using Finara.Domain.Common;

namespace Finara.Infrastructure.Automation;

// Zamanlanmış arka plan görevleri bir HTTP isteği içinde çalışmaz, dolayısıyla
// CurrentUserContext'in dayandığı IHttpContextAccessor'da kimlik yok. Bunun yerine
// mevcut servislere (SegmentationService, NextBestOfferService) DI dışında elle
// enjekte edilen bu "sistem" kimliği kullanılır: IsSuperAdmin=true olduğu için
// servisler zaten desteklediği "tüm bankaları kapsa" davranışına düşer — iş
// mantığında hiçbir değişiklik gerekmedi. UserId=null olduğundan denetim
// kayıtlarında "kim yaptı" alanı boş kalır (otomasyon tarafından yapıldığı,
// AuditLog.Details metnindeki [Otomasyon] etiketinden anlaşılır).
public class SystemCurrentUserContext : ICurrentUserContext
{
    public bool IsSuperAdmin => true;

    public Guid? BankId => null;

    public Guid? UserId => null;

    public Guid ResolveBankIdForWrite() => DefaultBank.Id;
}
