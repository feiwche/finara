import { APP_INITIALIZER, EnvironmentProviders, LOCALE_ID, makeEnvironmentProviders } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { providePrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { FinaraPreset } from './finara-preset';
import { ThemeService } from '../theme/theme.service';

// Client ve BackOffice aynı görsel kimliği paylaşsın diye PrimeNG tema kurulumu
// (FinaraPreset — Aura'nın üzerine kurulu marka katmanı, bkz. finara-preset.ts) ve TR
// locale/toast/confirm altyapısı tek yerden sağlanıyor — her iki app.config.ts bunu çağırır.
// '.app-dark' seçicisi ThemeService'teki DARK_CLASS ile birebir aynı olmalı.
export function provideFinaraUi(): EnvironmentProviders {
  return makeEnvironmentProviders([
    providePrimeNG({ theme: { preset: FinaraPreset, options: { darkModeSelector: '.app-dark' } } }),
    { provide: LOCALE_ID, useValue: 'tr' },
    MessageService,
    ConfirmationService,
    // ngx-translate: çeviri dosyaları her app'in kendi public/i18n/{dil}.json'undan
    // (statik varlık, favicon/og-image ile aynı klasör deseni) HTTP ile yükleniyor.
    // Varsayılan/geri düşme dili her zaman tr — kullanıcının gerçek tercihi giriş
    // yapıldıktan sonra UserPreferencesService tarafından uygulanıyor (bkz. AuthService).
    provideTranslateService({ lang: 'tr', fallbackLang: 'tr' }),
    provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }),
    // AuthService.restoreSession ile aynı desen (her iki app.config.ts'te tekrarlanmak
    // yerine burada merkezi — ThemeService hiçbir app'e özgü parametre almıyor) — route
    // guard'ları/ilk render'dan önce, kaydedilmiş tema tercihini <html>'e uyguluyor.
    {
      provide: APP_INITIALIZER,
      useFactory: (themeService: ThemeService) => () => themeService.init(),
      deps: [ThemeService],
      multi: true
    },
    // Varsayılan (tr) çeviri dosyası HTTP ile asenkron yükleniyor — bu tamamlanmadan
    // ilk render olursa (özellikle .instant() ile imperatif çağrılan metinlerde, ör.
    // hata mesajları) ham çeviri anahtarı görünürdü. Route guard'larından/ilk render'dan
    // önce tamamlanmasını bekliyoruz (ThemeService.init() ile aynı desen).
    {
      provide: APP_INITIALIZER,
      useFactory: (translate: TranslateService) => () => firstValueFrom(translate.use('tr')),
      deps: [TranslateService],
      multi: true
    }
  ]);
}
