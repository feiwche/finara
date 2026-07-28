import { Component, inject } from '@angular/core';
import { GuestLanguageService, LanguageCode } from './guest-language.service';

// Giriş/şifremi unuttum/şifre sıfırla ekranlarında kullanılan, kimliği doğrulanmamış
// ziyaretçi için dil seçici. Ayarlar sayfasındaki (giriş yapmış kullanıcı) dil seçiciden
// bilinçli olarak AYRI — bu bileşen backend'e hiç dokunmuyor, sadece GuestLanguageService
// üzerinden anlık arayüz dilini değiştirip localStorage'a yazıyor (bkz. o servisteki
// "manuallySet" gerekçesi).
@Component({
    selector: 'finara-language-switcher',
    template: `
    <div class="finara-lang-switcher" role="group" aria-label="Language / Sprache / Dil">
      @for (lang of languages; track lang.code) {
        <button
          type="button"
          class="finara-lang-switcher-btn"
          [class.active]="guestLanguage.currentLang() === lang.code"
          [attr.aria-pressed]="guestLanguage.currentLang() === lang.code"
          (click)="select(lang.code)"
        >{{ lang.label }}</button>
      }
    </div>
  `,
    styles: [`
    .finara-lang-switcher {
      display: inline-flex;
      gap: 2px;
      padding: 3px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.1);
      backdrop-filter: blur(6px);
      border: 1px solid rgba(255, 255, 255, 0.14);
    }

    .finara-lang-switcher-btn {
      border: none;
      background: transparent;
      color: rgba(255, 255, 255, 0.68);
      font-family: inherit;
      font-size: 0.78rem;
      font-weight: 600;
      letter-spacing: 0.03em;
      padding: 5px 12px;
      border-radius: 999px;
      cursor: pointer;
      transition: background-color 0.15s, color 0.15s;
    }

    .finara-lang-switcher-btn:hover {
      color: #ffffff;
      background: rgba(255, 255, 255, 0.1);
    }

    .finara-lang-switcher-btn.active {
      background: var(--p-amber-300, #EACC9E);
      color: #0D2A23;
    }
  `]
})
export class LanguageSwitcherComponent {
  protected readonly guestLanguage = inject(GuestLanguageService);

  protected readonly languages: Array<{ code: LanguageCode; label: string }> = [
    { code: 'tr', label: 'TR' },
    { code: 'en', label: 'EN' },
    { code: 'de', label: 'DE' }
  ];

  select(lang: LanguageCode): void {
    this.guestLanguage.setLanguage(lang);
  }
}
