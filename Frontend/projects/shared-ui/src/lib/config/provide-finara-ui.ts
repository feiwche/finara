import { APP_INITIALIZER, EnvironmentProviders, LOCALE_ID, makeEnvironmentProviders } from '@angular/core';
import { TitleStrategy } from '@angular/router';
import { providePrimeNG, PrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { FinaraPreset } from './finara-preset';
import { ThemeService } from '../theme/theme.service';
import { GuestLanguageService } from '../i18n/guest-language.service';
import { getPrimeNgTranslation } from '../i18n/primeng-locale';
import { registerTranslateServiceForEnumLabels } from '../utils/enum-labels.util';
import { registerTranslateServiceForApiErrors } from '../utils/api-error.util';
import { FinaraTitleStrategy } from '../i18n/finara-title-strategy';

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
    // Route'lardaki statik `title` yerine `data: { titleKey }` + bu strateji kullanılıyor —
    // sekme başlığının da dil değişince (sayfa yeniden yüklenmeden) güncellenmesi için.
    { provide: TitleStrategy, useClass: FinaraTitleStrategy },
    // ngx-translate: çeviri dosyaları her app'in kendi public/i18n/{dil}.json'undan
    // (statik varlık, favicon/og-image ile aynı klasör deseni) HTTP ile yükleniyor.
    // Varsayılan/geri düşme dili her zaman tr — giriş ekranındaki gerçek ziyaretçi
    // tercihi GuestLanguageService.init() ile, giriş yaptıktan sonraki gerçek tercih
    // UserPreferencesService ile uygulanıyor (bkz. AuthService).
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
    // Ziyaretçinin (giriş ekranında) daha önce seçtiği dili (varsa) route guard'larından/ilk
    // render'dan önce uygular — hiç seçmediyse varsayılan tr'de kalır. GuestLanguageService.init()
    // içeride translate.use() çağırdığı için ayrıca bir "tr'yi bekle" initializer'ına gerek yok.
    {
      provide: APP_INITIALIZER,
      useFactory: (guestLanguage: GuestLanguageService) => () => guestLanguage.init(),
      deps: [GuestLanguageService],
      multi: true
    },
    // PrimeNG'in KENDİ bileşenleri (Calendar ay/gün adları, Paginator etiketleri,
    // ConfirmDialog varsayılan Kabul Et/Vazgeç, boş tablo mesajı vb.) ngx-translate'ten
    // TAMAMEN BAĞIMSIZ kendi Translation nesnesini kullanıyor — hiç yapılandırılmazsa
    // PrimeNG'in gömülü varsayılanı (İngilizce) görünüyordu, uygulama dili başka olsa bile
    // (canlı testte ay adları/"First Page" hep İngilizce kalarak bulundu). Dil her
    // değiştiğinde (ister giriş ekranından, ister Ayarlar'dan) PrimeNG.setTranslation()
    // ile senkron tutuluyor.
    {
      provide: APP_INITIALIZER,
      useFactory: (translate: TranslateService, primeng: PrimeNG) => () => {
        primeng.setTranslation(getPrimeNgTranslation(translate.currentLang() ?? 'tr'));
        translate.onLangChange.subscribe(event => primeng.setTranslation(getPrimeNgTranslation(event.lang)));
        registerTranslateServiceForEnumLabels(translate);
        registerTranslateServiceForApiErrors(translate);
      },
      deps: [TranslateService, PrimeNG],
      multi: true
    }
  ]);
}
