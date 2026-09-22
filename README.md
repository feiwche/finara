# Finara

Bankalar için müşteri harcama segmentasyonu ve kampanya öneri platformu. Müşterilerin işlem
geçmişini RFM (Recency / Frequency / Monetary) modeliyle segmentlere ayırır, kural tabanlı bir
Next-Best-Offer motoruyla her müşteriye en uygun kampanyayı puanlar ve önerir; sonuçları
açıklanabilir, denetlenebilir bir arayüzde sunar.

> VakıfBank stajı kapsamında geliştirilmiştir. Sandbox/pilot amaçlı bir projedir, üretimde gerçek
> müşteri verisiyle çalışmaz.

## Mimari

- **Backend** — .NET 8, Clean Architecture (`Domain` / `Application` / `Infrastructure` / `Api`),
  EF Core + Dapper hibrit veri erişimi, SQL Server.
- **Frontend** — Angular 20 (SSR), tek bir monorepo altında üç proje: `client` (müşteri paneli),
  `backoffice` (banka yönetim paneli), `shared-ui` (ortak bileşen/servis kütüphanesi). PrimeNG 20,
  Angular Signals, ngx-translate (TR/EN/DE).
- **Yapay zeka** — Segment yorumları ve müşteri analizleri için yerel bir dil modeli (Ollama)
  kullanılır; hiçbir müşteri verisi üçüncü parti bir bulut AI sağlayıcısına gönderilmez. Karar
  motoru (RFM + Next-Best-Offer) her zaman birincil, açıklanabilir mekanizmadır — AI yalnızca
  destekleyici bir ikinci görüş sunar.
- **Kimlik doğrulama** — JWT (kısa ömürlü access token + rotasyonlu refresh token), isteğe bağlı
  2FA (TOTP), rol bazlı yetkilendirme (SuperAdmin / BankAdmin / Analyst), çok-kiracılı (multi-tenant)
  banka izolasyonu.

Ayrıntılı üretim/dağıtım notları için [Backend/Finara/PRODUCTION.md](Backend/Finara/PRODUCTION.md)
dosyasına bakın.

## Klasör Yapısı

```
Backend/Finara/
  Finara.Domain/              Saf iş mantığı, entity'ler, domain servisleri (DB'den bağımsız)
  Finara.Application/         Arayüzler, DTO/contract'lar
  Finara.Infrastructure/      EF Core, Dapper, dış servis entegrasyonları, otomasyon
  Finara.Api/                 Minimal API endpoint'leri, middleware, DI kompozisyonu
  Finara.Domain.Tests/        Birim testleri (domain katmanı)
  Finara.Infrastructure.Tests/Birim/entegrasyon testleri (SQLite in-memory)

Frontend/
  projects/client/            Müşteri paneli (Angular)
  projects/backoffice/        Banka yönetim paneli (Angular)
  projects/shared-ui/         İki uygulama arasında paylaşılan bileşen/servis/i18n kütüphanesi
  e2e/                        Playwright uçtan uca testleri

docker/
  docker-compose.yml          SQL Server (+ isteğe bağlı Redis) için yerel geliştirme ortamı
  backup-db.ps1 / restore-db.ps1
```

## Yerel Geliştirme

Gereksinimler: .NET 8 SDK, Node.js, Docker Desktop.

```bash
# 1) Veritabanını ayağa kaldır
docker compose -f docker/docker-compose.yml up -d

# 2) Backend
cd Backend/Finara/Finara.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=FinaraDb;User Id=sa;Password=<sifre>;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "<en az 32 karakterlik rastgele bir anahtar>"
dotnet run

# 3) Frontend (iki uygulama birden)
cd Frontend
npm install
npm run start-all   # client :4200, backoffice :4300
```

## Test

```bash
# Backend
cd Backend/Finara
dotnet test

# Frontend (birim testleri, vitest)
cd Frontend
npm test

# Frontend (Playwright E2E — backend + DB ayaktayken)
npm run test:e2e
```

## Güvenlik Notları

- Hiçbir gizli değer (`ConnectionStrings`, `Jwt:Key`, SMTP/Twilio kimlik bilgileri) repoda tutulmaz;
  geliştirmede `dotnet user-secrets`, üretimde ortam değişkenleri kullanılır — bkz.
  [PRODUCTION.md](Backend/Finara/PRODUCTION.md).
- `docker/.env` yalnızca yerel Docker container'ının SA şifresini içerir, git'e girmez.
