# shared-ui

`client` ve `backoffice` uygulamaları arasında paylaşılan ortak kütüphane — npm'e publish
edilmez, monorepo içi TypeScript path mapping (`tsconfig.json`) ile doğrudan tüketilir.

İçerdikleri:
- **API istemcisi** (`lib/api/finara-client.ts`) — backend'in Swagger şemasından NSwag ile
  otomatik üretilir (`npm run generate:api-client`), elle düzenlenmez.
- **Kimlik doğrulama** — `AuthService`, `authGuard`, `superAdminGuard`, `authInterceptor`.
- **i18n yardımcıları** — enum/hata kodu çevirisi, tarih/CSV biçimlendirme (`lib/utils/`).
- **Tema** — `ThemeService`, PrimeNG preset'i.
- **Gerçek zamanlı bildirimler** — `RealtimeService` (SignalR).
- **Ortak bileşenler** — dil değiştirici, sayfa başlığı stratejisi.

## Kod üretimi

```bash
ng generate component component-name --project shared-ui
```
`--project shared-ui` eklenmezse bileşen yanlışlıkla varsayılan projeye eklenir.

## Build

```bash
ng build shared-ui
```

## Test

```bash
ng test shared-ui
```
Test çalıştırıcısı **Vitest**'tir (Angular 20'nin `@angular/build:unit-test` builder'ı üzerinden;
Karma değil).
