import { Injectable, PLATFORM_ID, inject, signal, computed } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { Client, LoginRequest, RefreshRequest, RevokeRequest } from '../api/finara-client';

const REFRESH_TOKEN_KEY = 'finara_refresh_token';

// AuthService.IssueTokensAsync (backend) claim'i new Claim(ClaimTypes.Role, ...) ile
// ekliyor — JwtSecurityToken doğrudan (handler.CreateToken değil) oluşturulduğu için
// outbound kısaltma uygulanmıyor, JSON'da tam URI olarak görünüyor (canlı doğrulandı).
const ROLE_CLAIM_KEY = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

interface DecodedClaims {
  email: string | null;
  isSuperAdmin: boolean;
}

// JWT'nin sadece payload'ını (imza doğrulaması yapmadan) okuyoruz — burada güvenlik
// kararı verilmiyor (o backend'de), sadece arayüzde gösterilecek e-posta/rolü çıkarıyoruz.
function decodeClaimsFromJwt(token: string): DecodedClaims {
  try {
    const payload = token.split('.')[1];
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json);
    return { email: claims.email ?? null, isSuperAdmin: claims[ROLE_CLAIM_KEY] === 'SuperAdmin' };
  } catch {
    return { email: null, isSuperAdmin: false };
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly client = inject(Client);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly emailSignal = signal<string | null>(null);
  private readonly isSuperAdminSignal = signal(false);
  private refreshPromise: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);
  readonly email = this.emailSignal.asReadonly();
  readonly isSuperAdmin = this.isSuperAdminSignal.asReadonly();

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
    this.isSuperAdminSignal.set(false);
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
        this.isSuperAdminSignal.set(false);
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
    const claims = accessToken ? decodeClaimsFromJwt(accessToken) : { email: null, isSuperAdmin: false };
    this.emailSignal.set(claims.email);
    this.isSuperAdminSignal.set(claims.isSuperAdmin);
    if (this.isBrowser && refreshToken) {
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }
  }
}
