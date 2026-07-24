namespace Finara.Application.Interfaces;

// Faz 8.1 — multi-tenancy. Her istek için "kim işlem yapıyor" bilgisini servislere
// taşır: SuperAdmin için BankId null (tüm bankaları görür/varsayılan bankaya yazar),
// BankAdmin için kendi bankasının kimliği (sadece o bankayı görür/ona yazar).
public interface ICurrentUserContext
{
    bool IsSuperAdmin { get; }

    Guid? BankId { get; }

    // Denetim kaydına "kim yaptı" bilgisini damgalamak için — JWT'nin "sub" claim'inden
    // okunuyor. Auth uçları (login/register öncesi) gibi kimliği doğrulanmamış isteklerde null.
    Guid? UserId { get; }

    // Yazma işlemlerinde (yeni müşteri/kampanya/işlem oluşturma) hangi bankaya
    // damgalanacağını çözer: BankAdmin için kendi bankası, SuperAdmin için — henüz
    // banka seçme arayüzü olmadığı için — varsayılan banka.
    Guid ResolveBankIdForWrite();
}
