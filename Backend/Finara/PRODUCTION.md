# Prod Secrets Stratejisi

Bu depo, hiçbir gizli değeri (connection string, JWT anahtarı, SMTP/Twilio/Garanti kimlik bilgileri) doğrudan bir `appsettings*.json` dosyasında tutmaz. `appsettings.json` ve `appsettings.Production.json` yalnızca **sır olmayan**, ortam bazlı davranış anahtarlarını (ör. hangi bildirim sağlayıcısının aktif olduğu) içerir; gerçek değerler ortam değişkenlerinden (environment variables) okunur.

## Katmanlar ve öncelik sırası

ASP.NET Core'un yapılandırma sağlayıcıları, aşağıdaki sırayla üst üste biner (sonraki, öncekini ezer):

1. `appsettings.json` — tüm ortamlarda geçerli, sır içermeyen varsayılanlar.
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` — ortama özel, yine sır içermeyen davranış anahtarları (ör. `appsettings.Production.json`'daki `Notifications:Email:Provider = "Smtp"`).
3. **Geliştirmede:** `dotnet user-secrets` (yalnızca yerel makinede, repoya hiç girmez).
4. **Üretimde:** ortam değişkenleri — bu katman en yüksek önceliğe sahiptir ve gerçek sırların TEK kaynağıdır.

Ortam değişkeni adlandırması, iç içe JSON anahtarlarını `__` (çift alt çizgi) ile düzleştirir: `Jwt:Key` → `Jwt__Key`.

## Üretimde ortam değişkeni olarak sağlanması ZORUNLU olan anahtarlar

| Anahtar | Ortam değişkeni adı | Not |
|---|---|---|
| Veritabanı bağlantısı | `ConnectionStrings__DefaultConnection` | |
| JWT imzalama anahtarı | `Jwt__Key` | En az 32 karakter, rastgele üretilmeli |
| SMTP kimlik bilgileri | `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress`, `Smtp__EnableSsl` | `Notifications:Email:Provider=Smtp` iken zorunlu |
| Twilio kimlik bilgileri | `Twilio__AccountSid`, `Twilio__AuthToken`, `Twilio__FromNumber` | `Notifications:Sms:Provider=Twilio` iken zorunlu |
| Garanti BBVA sandbox | `Garanti__ClientId`, `Garanti__ClientSecret`, `Garanti__TokenUrl` | Sadece `GarantiSandbox` sağlayıcılı bir banka varsa gerekir |

## Neden bu kadar basit tutuldu

Bir Key Vault/Secrets Manager entegrasyonu (Azure Key Vault, AWS Secrets Manager vb.) daha sağlam bir çözüm olurdu, ama şu an tek bir dağıtım hedefi (ve tek bir ortam) olmadan bu entegrasyonu eklemek erken bir soyutlama olurdu. Ortam değişkenleri, konteyner tabanlı her dağıtım aracında (Docker, App Service, ECS vb.) zaten yerel olarak desteklenen, ek bağımlılık gerektirmeyen bir asgari standarttır. Gerçek bir üretim hedefi netleştiğinde bu doküman güncellenip bir secrets-manager entegrasyonuna geçilebilir.

## Çoklu-instance dağıtımında bilinen risk

Proje şu an tek-instance (tek sunucu/container) varsayımıyla çalışıyor. Bir load balancer arkasına birden fazla instance konulacaksa, aşağıdaki nokta önce ele alınmalı:

- **Hız sınırlama (rate limiting) bellek-içi** — `Program.cs`'teki `AddRateLimiter` yapılandırması `FixedWindowRateLimiter` kullanıyor, sayaçlar her instance'ın kendi belleğinde tutuluyor. İki instance arkasına bir load balancer konursa, gerçek limit (ör. dakikada 100) instance sayısına bölünmüş gibi davranır — kullanıcı fark etmeden daha erken 429 alabilir ya da (round-robin dağılıma göre) daha gevşek bir limitle karşılaşabilir. Gerçek bir çözüm dağıtık bir sayaç deposu (ör. Redis) gerektirir — bu, yeni bir altyapı bağımlılığı eklemek anlamına geldiği için bilinçli olarak ertelendi.

**Data Protection anahtar deposu artık çoklu-instance güvenli (ÇÖZÜLDÜ).** 2FA sırlarını şifreleyen anahtar halkası önceden `%LOCALAPPDATA%/~/.aspnet` altında tek-makine bazlıydı; artık `Finara.Api/Persistence/DataProtectionKeysDbContext.cs` üzerinden veritabanında (zaten tüm instance'lar arasında paylaşılan tek kaynak — `DataProtectionKeys` tablosu, migration `AddDataProtectionKeys`) tutuluyor. Önceki oturumda bu bir CS1061 derleme hatasıyla çözülemiyordu; **kök neden basitti: `Program.cs`'te `using Microsoft.AspNetCore.DataProtection;` eksikti** — `SetApplicationName`/`PersistKeysToDbContext` gibi `IDataProtectionBuilder` üzerindeki genişletme metotları bu namespace `using` olmadan derleyici tarafından bulunamıyordu (önceki oturumda bu bir FrameworkReference/PackageReference tip çakışması sanılmıştı, değildi). Ayrıca `Microsoft.AspNetCore.DataProtection(.EntityFrameworkCore)` paketleri, kurulu SDK'daki `Microsoft.AspNetCore.App.Ref` (8.0.29) ile aynı patch sürümüne (8.0.29) hizalandı — önceki `8.0.11` pin'i gereksiz bir sürüm sapması yaratıyordu (zararsızdı ama gürültülüydü).

Ayrı bir `DataProtectionKeysDbContext` (Finara.Infrastructure'daki `FinaraDbContext`'in İÇİNDE değil, Web SDK olan `Finara.Api` projesinde) kullanılmasının nedeni `DataProtectionKeysDbContext.cs`'teki yorumda belgelendi — aynı bağlantı dizesini kullanır, tek bir ek tablo dışında bir etkisi yoktur.

**Doğrulama (gerçek, uçtan uca):** Migration DB'ye uygulandı, backend başlatıldı ve gerçek bir Data Protection anahtarının `DataProtectionKeys` tablosuna yazıldığı doğrulandı; backend yeniden başlatıldı ve anahtar sayısının 1'de kaldığı (yeni bir anahtar ÜRETİLMEDİĞİ, var olanın yeniden kullanıldığı — çoklu-instance senaryosunun tam olarak ihtiyaç duyduğu davranış) görüldü. Gerçek TOTP algoritmasıyla (Node.js RFC 6238 betiği) `superadmin@finara.dev` hesabında uçtan uca 2FA turu (setup → confirm → giriş → 2FA meydan okuma → doğru kodla verify-2fa → gerçek token → disable) çalıştırıldı, hepsi başarılı. 177/177 backend testi yeşil. Hesap test sonrası orijinal (2FA kapalı) haline döndürüldü.

## Veritabanı yedekleme ve felaket kurtarma

Veri, Docker'da adlandırılmış bir volume'de tutuluyor (`docker/docker-compose.yml` → `finara-mssql-data`). Bu volume container'lar arasında kalıcıdır ama tek makineye bağlıdır — o makine/disk kaybedilirse veri de kaybedilir. Bu yüzden ayrı, taşınabilir bir yedekleme mekanizması gerekli.

**Araçlar:** `docker/backup-db.ps1` ve `docker/restore-db.ps1`.

- `backup-db.ps1`: konteyner içinde `sqlcmd` ile SQL Server'ın kendi `BACKUP DATABASE` komutunu çalıştırır (canlı, bağlantıları kesmeden alınan bir "hot backup"; docker-compose'daki `MSSQL_PID=Express` sıkıştırmayı desteklemediği için `WITH COMPRESSION` kullanılmadı), üretilen `.bak` dosyasını `docker cp` ile `docker/backups/` altına taşır ve konteynerdeki geçici kopyayı siler. `-KeepLast` parametresiyle (varsayılan 14) eski yedekleri otomatik temizler.
- `restore-db.ps1 -BakFile <yol>`: seçilen `.bak` dosyasını konteynere kopyalar, veritabanını `SINGLE_USER` moduna alıp `WITH REPLACE` ile geri yükler. **Hedef veritabanının üzerine yazar** — bu yüzden çalıştırmadan önce bir onay bekletir.

```powershell
# Manuel yedek
./docker/backup-db.ps1

# Geri yükleme (dikkat: FinaraDb'nin üzerine yazar)
./docker/restore-db.ps1 -BakFile ./docker/backups/FinaraDb_20260727_153000.bak
```

`docker/backups/` gerçek veri içerdiği için `.gitignore`'a eklendi — repoya hiç girmez.

**Otomatik zamanlama:** Şu an script'ler manuel çalıştırılıyor. Üretimde Windows Task Scheduler (`schtasks /create /sc daily ...`) veya Linux'ta bir `cron` girdisiyle `backup-db.ps1`'in günlük çalıştırılması yeterli; ek bir yedekleme servisi/aracı (ör. Azure Backup, Velero) şu an tek bir dağıtım hedefi netleşmeden erken bir yatırım olurdu — [Neden bu kadar basit tutuldu](#neden-bu-kadar-basit-tutuldu) bölümündeki gerekçeyle aynı mantık.

**Kapsam dışı (bilinçli):** Yedekler bu haliyle konteynerin bulunduğu makinede tutuluyor — coğrafi olarak ayrı bir depoya (ör. S3/Blob Storage) otomatik yükleme yapılmıyor. Gerçek bir üretim ortamında `docker/backups/` klasörünün düzenli olarak uzak bir depoya senkronize edilmesi (ör. `rclone`, `azcopy`) eklenmeli; bu adım somut bir bulut sağlayıcısı seçilene kadar ertelendi.

## Kontrol listesi (yeni bir ortama dağıtımdan önce)

- [ ] Yukarıdaki tüm ortam değişkenleri, dağıtım ortamının kendi mekanizmasıyla (Docker secrets, App Service Configuration, CI/CD secret store vb.) ayarlandı — hiçbiri dosyaya yazılmadı.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` ayarlandı.
- [ ] `dotnet user-secrets` yalnızca geliştirme makinelerinde kullanılıyor, üretim sunucusunda hiç çalıştırılmadı.
- [ ] `/health/ready` ucu, veritabanı bağlantısının kurulduğunu doğruluyor (bkz. `Program.cs`).
