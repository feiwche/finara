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
  role: string | null;
}

// JWT'nin sadece payload'ını (imza doğrulaması yapmadan) okuyoruz — burada güvenlik
// kararı verilmiyor (o backend'de), sadece arayüzde gösterilecek e-posta/rolü çıkarıyoruz.
function decodeClaimsFromJwt(token: string): DecodedClaims {
  try {
    const payload = token.split('.')[1];
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const claims = JSON.parse(json);
    return { email: claims.email ?? null, role: claims[ROLE_CLAIM_KEY] ?? null };
  } catch {
    return { email: null, role: null };
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly client = inject(Client);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly emailSignal = signal<string | null>(null);
  private readonly roleSignal = signal<string | null>(null);
  private refreshPromise: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this.accessTokenSignal() !== null);
  readonly email = this.emailSignal.asReadonly();
  readonly role = this.roleSignal.asReadonly();
  readonly isSuperAdmin = computed(() => this.roleSignal() === 'SuperAdmin');
  readonly isAnalyst = computed(() => this.roleSignal() === 'Analyst');

  // Analist (salt-okunuz) rolü hariç herkes yazma işlemi yapabilir — mutasyon
  // butonlarını göstermeden önce bu kontrol ediliyor (backend zaten "WriteAccess"
  // policy'siyle aynı kısıtlamayı zorunlu kılıyor, bu sadece UX katmanı).
  readonly canWrite = computed(() => !this.isAnalyst());

  get accessToken(): string | null {
    return this.accessTokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const result = await firstValueFrom(this.client.login(new LoginRequest({ email, password })));
    this.applyTokens(result.accessToken, result.refreshToken);
  }

  async logout(): Promise<void> {
    const refreshToken = this.isBrowser ? localStorage.getItem(REFRESH_TOKEN_KEY) : null;
    this.clearLocalSession();
    if (refreshToken) {
      await firstValueFrom(this.client.revoke(new RevokeRequest({ refreshToken })));
    }
  }

  // Kendi tüm oturumlarını (tüm cihazlardaki refresh token'ları) sonlandırır — backend
  // kullanıcıyı JWT'den (ICurrentUserContext) tanıdığı için ayrı bir refreshToken
  // parametresi gerekmiyor, sadece geçerli bir access token yeterli.
  async logoutAllDevices(): Promise<void> {
    await firstValueFrom(this.client.revokeAll());
    this.clearLocalSession();
  }

  private clearLocalSession(): void {
    this.accessTokenSignal.set(null);
    this.emailSignal.set(null);
    this.roleSignal.set(null);
    if (this.isBrowser) {
      localStorage.removeItem(REFRESH_TOKEN_KEY);
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
        this.clearLocalSession();
        throw err;
      } finally {
        this.refreshPromise = null;
      }
    })();

    return this.refreshPromise;
  }

  private applyTokens(accessToken: string | undefined, refreshToken: string | undefined): void {
    this.accessTokenSignal.set(accessToken ?? null);
    const claims = accessToken ? decodeClaimsFromJwt(accessToken) : { email: null, role: null };
    this.emailSignal.set(claims.email);
    this.roleSignal.set(claims.role);
    if (this.isBrowser && refreshToken) {
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    }
  }
}
