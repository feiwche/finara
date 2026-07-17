import { APP_INITIALIZER, ApplicationConfig, LOCALE_ID, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideClientHydration } from '@angular/platform-browser';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { providePrimeNG } from 'primeng/config';
import { ConfirmationService, MessageService } from 'primeng/api';
import Aura from '@primeng/themes/aura';

import { routes } from './app.routes';
import { API_BASE_URL, Client } from './api/finara-client';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideClientHydration(),
    provideAnimationsAsync(),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: false } } }),
    { provide: LOCALE_ID, useValue: 'tr' },
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    Client,
    // Toast (p-toast) ve onay dialogu (p-confirmdialog) kök seviyede sağlanıyor ki
    // login dahil her route bunları kullanabilsin (bkz. app.component.html).
    MessageService,
    ConfirmationService,
    // Route guard'ları değerlendirilmeden önce localStorage'daki refresh token ile
    // oturumu sessizce geri yükler — yoksa bir sayfa yenilemesinde authGuard, henüz
    // restoreSession tamamlanmadan isAuthenticated=false görüp /login'e atardı.
    {
      provide: APP_INITIALIZER,
      useFactory: (authService: AuthService) => () => authService.restoreSession(),
      deps: [AuthService],
      multi: true
    }
  ]
};
