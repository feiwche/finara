import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';

import { routes } from './app.routes';
import { API_BASE_URL, Client } from './api/finara-client';
import { authInterceptor } from './core/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideClientHydration(),
    provideAnimationsAsync(),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: false } } }),
    // Faz 3.5 walking skeleton: API her zaman localhost:5068'de (bkz. launchSettings.json).
    // Faz 6'da ortam bazlı (environment.ts) yapılandırmaya geçilecek.
    { provide: API_BASE_URL, useValue: 'http://localhost:5068' },
    Client
  ]
};
