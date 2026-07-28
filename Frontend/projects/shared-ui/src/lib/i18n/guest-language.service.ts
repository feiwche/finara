import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';

const GUEST_LANGUAGE_KEY = 'finara_guest_language';
export const SUPPORTED_LANGUAGE_CODES = ['tr', 'en', 'de'] as const;
export type LanguageCode = (typeof SUPPORTED_LANGUAGE_CODES)[number];

// Giriş ekranındaki dil seçici (henüz kimliği doğrulanmamış bir ziyaretçi için) ile
// UserPreferencesService'in (giriş yapıldıktan sonraki, backend'e kalıcı) dil yönetimi
// arasındaki köprü. localStorage'da SADECE ziyaretçinin bilinçli seçimini tutar — bu,
// ThemeService'in "sadece kullanıcının açık niyeti yazılır" ilkesiyle aynı desen.
//
// `manuallySet`, kullanıcının BU oturumda giriş ekranındaki seçiciye dokunup dokunmadığını
// ayırt eder: dokunduysa (ör. Almanca seçti) bu tercih, girişten sonra UserPreferencesService
// tarafından backend'deki (muhtemelen farklı) kayıtlı tercihin ÖNÜNE geçirilip backend'e de
// yazılır ("giriş ekranında seçilen dille devam" isteği). Dokunmadıysa, backend'deki kayıtlı
// tercih normal şekilde uygulanır (ör. döner bir kullanıcının önceden Ayarlar'dan seçtiği dil
// kaybolmaz).
@Injectable({ providedIn: 'root' })
export class GuestLanguageService {
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly manuallySetFlag = signal(false);
  readonly currentLang = signal<LanguageCode>('tr');

  // APP_INITIALIZER'dan çağrılıyor — route guard'larından/ilk render'dan önce, daha önce
  // ziyaretçinin seçtiği dili (varsa) uygular.
  init(): Promise<unknown> {
    const stored = this.getPersisted();
    this.currentLang.set(stored);
    return firstValueFrom(this.translate.use(stored));
  }

  getPersisted(): LanguageCode {
    if (!this.isBrowser) return 'tr';
    const stored = localStorage.getItem(GUEST_LANGUAGE_KEY);
    return stored && (SUPPORTED_LANGUAGE_CODES as readonly string[]).includes(stored) ? (stored as LanguageCode) : 'tr';
  }

  // Giriş ekranındaki seçiciden çağrılır.
  setLanguage(lang: LanguageCode): void {
    this.manuallySetFlag.set(true);
    this.currentLang.set(lang);
    if (this.isBrowser) localStorage.setItem(GUEST_LANGUAGE_KEY, lang);
    void this.translate.use(lang);
  }

  // AuthService/UserPreferencesService'in login sonrası tek seferlik okuması — bu oturumda
  // gerçekten bilinçli bir seçim yapıldıysa o dili döner ve bayrağı sıfırlar (bir sonraki
  // login'i etkilemesin diye), yapılmadıysa null döner (normal backend-yükleme akışı işler).
  consumeManualSelection(): LanguageCode | null {
    if (!this.manuallySetFlag()) return null;
    this.manuallySetFlag.set(false);
    return this.currentLang();
  }

  // Çıkış yapıldığında çağrılıyor — hesaba özel tercih sıfırlanır ama ziyaretçinin login
  // ekranı için en son seçtiği dil (varsa) korunur, sıfır/Türkçe'ye zorla dönülmez.
  resetToGuestDefault(): void {
    const guestLang = this.getPersisted();
    this.currentLang.set(guestLang);
    void this.translate.use(guestLang);
  }
}
