using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Finara.Api.Persistence;

// Data Protection anahtar halkasının (2FA sırlarını şifreleyen) tek amaçlı kalıcı deposu.
// Bilinçli olarak Finara.Infrastructure/FinaraDbContext'in İÇİNE değil, Web SDK olan bu
// projeye kondu: Microsoft.AspNetCore.DataProtection.EntityFrameworkCore paketi Web SDK
// olmayan bir projeden (ProjectReference zinciriyle) kullanılınca, paketin kendi bağımlı
// olduğu Microsoft.AspNetCore.DataProtection (çekirdek) paketi Microsoft.AspNetCore.App
// paylaşılan çerçevesiyle aynı adı taşıyan AYRI bir derleme olarak kalıyor — NuGet'in bu
// paketleri paylaşılan çerçeveyle birleştirme (trimming) mekanizması sadece paketin
// DOĞRUDAN bir Web SDK projesinden referans edildiği durumda çalışıyor. Bu yüzden
// PersistKeysToFileSystem/PersistKeysToDbContext gibi IDataProtectionBuilder üzerindeki
// genişletme metotları CS1061 ile çözülemiyordu (aynı isimli iki farklı IDataProtectionBuilder
// tipi). Aynı paketi doğrudan Finara.Api'ye (Microsoft.NET.Sdk.Web) eklemek bu çakışmayı ortadan
// kaldırıyor. Aynı bağlantı dizesini (appsettings/user-secrets'taki ConnectionStrings:DefaultConnection)
// kullanır, ayrı bir tablo (DataProtectionKeys) dışında uygulamanın geri kalanıyla hiç etkileşmez.
public class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
