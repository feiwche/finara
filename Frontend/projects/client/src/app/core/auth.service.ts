import { Injectable, PLATFORM_ID, inject, signal, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { Client, LoginRequest, RefreshRequest, RevokeRequest } from '../api/finara-client';

const REFRESH_TOKEN_KEY = 'finara_refresh_token';

// JWT'nin sadece payload'ını (imza doğrulaması yapmadan) okuyoruz — burada güvenlik
// kararı verilmiyor (o backend'de), sadece arayüzde gösterilecek e-postayı çıkarıyoruz.
function decodeEmailFromJwt(token: string): string | null {
  try {
    const payload = token.split('.')[1];
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json).email ?? null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly client = inject(Client);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly emailSignal = signal<string | null>(null);
  private refreshPromise: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);
  readonly email = this.emailSignal.asReadonly();

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const result = await firstValueFrom(this.client.login(new LoginRequest({ email, password })));
    this.applyTokens(result.accessToken, result.refreshToken);
  }

  async logout(): Promise<void> {
    const refreshToken = this.isBrowser ? localStorage.getItem(REFRESH_TOKEN_KEY) : null;
    this.accessTokenSignal.set(null);
    this.emailSignal.set(null);
    if (this.isBrowser) {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
    }
    if (refreshToken) {
      await firstValueFrom(this.client.revoke(new RevokeRequest({ refreshToken })));
    }
  }

  // Sayfa yenilendiğinde access token bellekten (signal) kaybolur ama refresh token
  // localStorage'da kalıcı — uygulama açılışında bunu kullanarak oturumu sessizce
  // geri yükler. Bulunamazsa/başarısız olursa sessizce login ekranında kalınır.
  async restoreSession(): Promise<void> {
    if (!this.isBrowser) return;
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return;

    try {
      await this.ensureFreshAccessToken();
    } catch {
      // Refresh token süresi dolmuş/iptal edilmiş — sessizce login ekranına düş.
    }
  }

  // 401 sonrası interceptor'ın çağırdığı yenileme — eşzamanlı birden fazla 401 aynı anda
  // gelirse (paralel istekler) hepsi aynı tek refresh çağrısını paylaşsın diye
  // in-flight promise cache'leniyor (refresh rotasyonu yüzünden ikinci bağımsız çağrı,
  // ilk çağrının zaten geçersiz kıldığı eski token'ı kullanıp başarısız olurdu).
  async ensureFreshAccessToken(): Promise<string | null> {
    if (this.refreshPromise) return this.refreshPromise;

    const refreshToken = this.isBrowser ? localStorage.getItem(REFRESH_TOKEN_KEY) : null;
    if (!refreshToken) return null;

    this.refreshPromise = (async () => {
      try {
        const result = await firstValueFrom(this.client.refresh(new RefreshRequest({ refreshToken })));
        this.applyTokens(result.accessToken, result.refreshToken);
        return result.accessToken ?? null;
      } catch (err) {
        this.accessTokenSignal.set(null);
        this.emailSignal.set(null);
        if (this.isBrowser) localStorage.removeItem(REFRESH_TOKEN_KEY);
        throw err;
      } finally {
        this.refreshPromise = null;
      }
    })();

    return this.refreshPromise;
  }

  private applyTokens(accessToken: string | undefined, refreshToken: string | undefined): void {
    this.accessTokenSignal.set(accessToken ?? null);
    this.emailSignal.set(accessToken ? decodeEmailFromJwt(accessToken) : null);
    if (this.isBrowser && refreshToken) {
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }
  }
}
