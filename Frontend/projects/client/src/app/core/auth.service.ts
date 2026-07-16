import { Injectable, PLATFORM_ID, inject, signal, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { Client, LoginRequest, RevokeRequest } from '../api/finara-client';

const REFRESH_TOKEN_KEY = 'finara_refresh_token';

// Faz 3.5 walking skeleton kapsamında bilerek basit tutuldu: access token yalnızca
// bellekte (signal), refresh token localStorage'da. Sessiz yenileme (silent refresh),
// gerçek müşteri/backoffice giriş akışları Faz 6/7'de ele alınacak.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly client = inject(Client);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly accessTokenSignal = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const result = await firstValueFrom(this.client.login(new LoginRequest({ email, password })));
    this.accessTokenSignal.set(result.accessToken ?? null);
    if (this.isBrowser && result.refreshToken) {
      localStorage.setItem(REFRESH_TOKEN_KEY, result.refreshToken);
    }
  }

  async logout(): Promise<void> {
    const refreshToken = this.isBrowser ? localStorage.getItem(REFRESH_TOKEN_KEY) : null;
    this.accessTokenSignal.set(null);
    if (this.isBrowser) {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    }
    if (refreshToken) {
      await firstValueFrom(this.client.revoke(new RevokeRequest({ refreshToken })));
    }
  }
}
