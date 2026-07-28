import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';

import { Client, UiLanguage, UpdateUserPreferencesRequest, UserPreferencesDto } from '../api/finara-client';

// ngx-translate dosyaları (public/i18n/*.json) UiLanguage enum değerleriyle DEĞİL, kısa
// dil koduyla adlandırılıyor (tr/en/de) — ikisi arasındaki tek eşleme noktası burası.
const LANGUAGE_CODE_MAP: Record<UiLanguage, string> = {
  [UiLanguage.Turkish]: 'tr',
  [UiLanguage.English]: 'en',
  [UiLanguage.German]: 'de'
};

// Kullanıcının kişisel arayüz tercihleri (dil, bildirim, tarih/saat, tablo görünümü) —
// backend'de User.Preferences'a (JSON kolon) karşılık gelir. ThemeService'in aksine
// (o sadece localStorage, cihaz bazlı) bu tercihler backend'de kalıcı ve cihazlar arası
// senkron — bu yüzden sadece giriş yapıldığında yüklenip uygulanıyor (bkz. AuthService).
@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly client = inject(Client);
  private readonly translate = inject(TranslateService);

  readonly preferences = signal<UserPreferencesDto | null>(null);

  async load(): Promise<void> {
    try {
      const prefs = await firstValueFrom(this.client.preferencesGET());
      this.preferences.set(prefs);
      this.applyLanguage(prefs.language);
    } catch {
      // Giriş yapılmamış ya da geçici bir hata — sessizce varsayılan dilde (tr) kalınır.
    }
  }

  async save(request: UpdateUserPreferencesRequest): Promise<UserPreferencesDto> {
    const updated = await firstValueFrom(this.client.preferencesPUT(request));
    this.preferences.set(updated);
    this.applyLanguage(updated.language);
    return updated;
  }

  // Çıkış yapıldığında çağrılıyor — paylaşılan bir cihazda bir sonraki kullanıcının
  // (ya da giriş ekranının) önceki kullanıcının seçtiği dilde kalmaması için varsayılana
  // (tr) dönülüyor.
  reset(): void {
    this.preferences.set(null);
    void this.translate.use(LANGUAGE_CODE_MAP[UiLanguage.Turkish]);
  }

  private applyLanguage(language: UiLanguage | undefined): void {
    void this.translate.use(LANGUAGE_CODE_MAP[language ?? UiLanguage.Turkish]);
  }
}
