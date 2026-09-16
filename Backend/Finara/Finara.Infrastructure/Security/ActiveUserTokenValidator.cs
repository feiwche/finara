using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Security;

// Program.cs'teki JwtBearerEvents.OnTokenValidated'ın çekirdek mantığı buraya çıkarıldı —
// satır içi bir lambda birim testiyle doğrudan doğrulanamıyordu (denetimde bulundu: anlık
// hesap kilitleme, bu projenin en kritik güvenlik davranışlarından biri olmasına rağmen
// hiçbir otomatik testle korunmuyordu, yalnızca manuel/canlı testle doğrulanmıştı).
//
// Hesap devre dışı bırakıldığında ya da oturumları zorla sonlandırıldığında yalnızca refresh
// token'lar iptal ediliyordu; JWT stateless olduğu için ELİNDEKİ access token süresi dolana
// kadar (varsayılan 15 dk) her yere erişmeye devam edebiliyordu. Bu sınıf her kimliği
// doğrulanmış istekte bir birincil-anahtar araması yapıyor (indeksli, milisaniyenin altı) —
// maliyeti bilinçli kabul edildi: bankacılıkta "devre dışı bıraktım ama 15 dakika daha
// girebiliyor" savunulabilir değil.
public class ActiveUserTokenValidator(FinaraDbContext dbContext)
{
    public async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var isActive = await dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return isActive == true;
    }
}
