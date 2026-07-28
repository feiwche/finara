import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';

import { Client, UiLanguage, UpdateUserPreferencesRequest, UserPreferencesDto } from '../api/finara-client';
import { GuestLanguageService, LanguageCode } from '../i18n/guest-language.service';

// ngx-translate dosyaları (public/i18n/*.json) UiLanguage enum değerleriyle DEĞİL, kısa
// dil koduyla adlandırılıyor (tr/en/de) — ikisi arasındaki tek eşleme noktası burası.
const LANGUAGE_CODE_MAP: Record<UiLanguage, LanguageCode> = {
  [UiLanguage.Turkish]: 'tr',
  [UiLanguage.English]: 'en',
  [UiLanguage.German]: 'de'
};

const CODE_TO_LANGUAGE_MAP: Record<LanguageCode, UiLanguage> = {
  tr: UiLanguage.Turkish,
  en: UiLanguage.English,
  de: UiLanguage.German
};

// Kullanıcının kişisel arayüz tercihleri (dil, bildirim, tarih/saat, tablo görünümü) —
// backend'de User.Preferences'a (JSON kolon) karşılık gelir. ThemeService'in aksine
// (o sadece localStorage, cihaz bazlı) bu tercihler backend'de kalıcı ve cihazlar arası
// senkron — bu yüzden sadece giriş yapıldığında yüklenip uygulanıyor (bkz. AuthService).
@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly client = inject(Client);
  private readonly translate = inject(TranslateService);
  private readonly guestLanguage = inject(GuestLanguageService);

  readonly preferences = signal<UserPreferencesDto | null>(null);

  // Girişten sonra çağrılıyor (login/demoLogin/verifyTwoFactor/restoreSession). Ziyaretçi
  // giriş ekranında BİLİNÇLİ olarak bir dil seçtiyse (bkz. GuestLanguageService) bu tercih,
  // backend'deki kayıtlı dilin ÖNÜNE geçirilip backend'e de yazılır — "giriş ekranında
  // seçilen dille devam edilsin" isteği. Seçilmediyse backend'deki kayıtlı tercih normal
  // şekilde uygulanır (dönen bir kullanıcının Ayarlar'dan seçtiği dil kaybolmaz).
  async load(): Promise<void> {
    try {
      const prefs = await firstValueFrom(this.client.preferencesGET());
      const manualCode = this.guestLanguage.consumeManualSelection();
      const manualLanguage = manualCode ? CODE_TO_LANGUAGE_MAP[manualCode] : null;

      if (manualLanguage !== null && manualLanguage !== prefs.language) {
        await this.save(new UpdateUserPreferencesRequest({ ...prefs, language: manualLanguage }));
        return;
      }

      this.preferences.set(prefs);
      this.applyLanguage(prefs.language);
    } catch {
      // Giriş yapılmamış ya da geçici bir hata — sessizce mevcut dilde kalınır.
    }
  }

  async save(request: UpdateUserPreferencesRequest): Promise<UserPreferencesDto> {
    const updated = await firstValueFrom(this.client.preferencesPUT(request));
    this.preferences.set(updated);
    this.applyLanguage(updated.language);
    return updated;
  }

  // Çıkış yapıldığında çağrılıyor — hesaba özel tercih sıfırlanır, arayüz dili ziyaretçinin
  // giriş ekranı için en son seçtiği dile (varsa, yoksa tr) döner (bkz. GuestLanguageService).
  reset(): void {
    this.preferences.set(null);
    this.guestLanguage.resetToGuestDefault();
  }

  private applyLanguage(language: UiLanguage | undefined): void {
    void this.translate.use(LANGUAGE_CODE_MAP[language ?? UiLanguage.Turkish]);
  }
}
