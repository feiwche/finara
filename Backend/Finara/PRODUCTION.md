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

## Çoklu-instance dağıtımında bilinen iki açık risk

Proje şu an tek-instance (tek sunucu/container) varsayımıyla çalışıyor. Bir load balancer arkasına birden fazla instance konulacaksa, aşağıdaki iki nokta önce ele alınmalı:

1. **Hız sınırlama (rate limiting) bellek-içi** — `Program.cs`'teki `AddRateLimiter` yapılandırması `FixedWindowRateLimiter` kullanıyor, sayaçlar her instance'ın kendi belleğinde tutuluyor. İki instance arkasına bir load balancer konursa, gerçek limit (ör. dakikada 100) instance sayısına bölünmüş gibi davranır — kullanıcı fark etmeden daha erken 429 alabilir ya da (round-robin dağılıma göre) daha gevşek bir limitle karşılaşabilir. Gerçek bir çözüm dağıtık bir sayaç deposu (ör. Redis) gerektirir — bu, yeni bir altyapı bağımlılığı eklemek anlamına geldiği için bilinçli olarak ertelendi.
2. **Data Protection anahtar deposu tek-makine bazlı** — 2FA sırlarını şifreleyen anahtar halkası varsayılan olarak `%LOCALAPPDATA%/~/.aspnet` altında, sadece o makinede duruyor. Bir container yeniden dağıtılırsa ya da ikinci bir instance açılırsa, o instance'ın ürettiği/okuduğu şifreli 2FA sırları diğer instance'larla eşleşmez. Bu turda `PersistKeysToFileSystem`/`PersistKeysToDbContext` ile paylaşımlı bir depoya geçirilmeye çalışıldı ama net8.0 Web SDK + `Microsoft.AspNetCore.App.Ref 8.0.29` kombinasyonunda `IDataProtectionBuilder`'ın bu genişletme metotlarıyla CS1061 hatasıyla çözülemediği bulundu (muhtemelen FrameworkReference/PackageReference tip kimliği çakışması) — kökü bu oturumda bulunamadı, geri alındı. **Sıradaki adım:** ya bu derleme sorununu ayrı bir oturumda araştırıp çözmek, ya da doğrudan `ProtectKeysWithCertificate` + paylaşımlı bir volume ile ilerlemek.

## Kontrol listesi (yeni bir ortama dağıtımdan önce)

- [ ] Yukarıdaki tüm ortam değişkenleri, dağıtım ortamının kendi mekanizmasıyla (Docker secrets, App Service Configuration, CI/CD secret store vb.) ayarlandı — hiçbiri dosyaya yazılmadı.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` ayarlandı.
- [ ] `dotnet user-secrets` yalnızca geliştirme makinelerinde kullanılıyor, üretim sunucusunda hiç çalıştırılmadı.
- [ ] `/health/ready` ucu, veritabanı bağlantısının kurulduğunu doğruluyor (bkz. `Program.cs`).
