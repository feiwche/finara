import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

const THEME_KEY = 'finara_theme';
// PrimeNG'in providePrimeNG({ theme: { options: { darkModeSelector } } }) ayarıyla
// (provide-finara-ui.ts) birebir aynı seçici olmalı — biri değişirse diğeri de değişmeli.
const DARK_CLASS = 'app-dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly isDark = signal(false);

  // APP_INITIALIZER'dan (bkz. provide-finara-ui.ts) route guard'lardan/ilk render'dan
  // önce çağrılıyor — kullanıcı daha önce karanlık modu seçtiyse sayfa hep açık temayla
  // "yanıp" sonra kararmasın diye. SSR'da (isBrowser=false) localStorage erişilemez,
  // sunucu her zaman açık temayla render eder — client'ta hydration sonrası kısa bir
  // geçiş olabilir (kabul edilen tradeoff, bkz. Madde 8/8 notu).
  init(): void {
    if (!this.isBrowser) return;
    const stored = localStorage.getItem(THEME_KEY);
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    this.apply(stored ? stored === 'dark' : prefersDark);
  }

  toggle(): void {
    this.apply(!this.isDark());
  }

  private apply(dark: boolean): void {
    this.isDark.set(dark);
    if (!this.isBrowser) return;
    document.documentElement.classList.toggle(DARK_CLASS, dark);
    localStorage.setItem(THEME_KEY, dark ? 'dark' : 'light');
  }
}
