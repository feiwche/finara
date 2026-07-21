import { EnvironmentProviders, LOCALE_ID, makeEnvironmentProviders } from '@angular/core';
import { providePrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';
import { FinaraPreset } from './finara-preset';

// Client ve BackOffice aynı görsel kimliği paylaşsın diye PrimeNG tema kurulumu
// (FinaraPreset — Aura'nın üzerine kurulu marka katmanı, bkz. finara-preset.ts —
// + karanlık mod kilidi) ve TR locale/toast/confirm altyapısı tek yerden sağlanıyor —
// her iki app.config.ts bunu çağırır.
export function provideFinaraUi(): EnvironmentProviders {
  return makeEnvironmentProviders([
    providePrimeNG({ theme: { preset: FinaraPreset, options: { darkModeSelector: false } } }),
    { provide: LOCALE_ID, useValue: 'tr' },
    MessageService,
    ConfirmationService
  ]);
}
