import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { of } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthResult, Client, LoginResult } from '../api/finara-client';

const ROLE_CLAIM_KEY = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

// decodeClaimsFromJwt sadece payload'ı base64url çözüp JSON.parse ediyor (imza doğrulaması
// yapmıyor) — testte gerçek bir imzaya ihtiyaç yok, sadece doğru şekilde encode edilmiş bir
// payload segmenti yeterli.
function fakeJwt(claims: Record<string, unknown>): string {
  const base64Url = (obj: object) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${base64Url({ alg: 'HS256' })}.${base64Url(claims)}.fake-signature`;
}

describe('AuthService', () => {
  let client: jest.Mocked<Pick<Client,
    'login' | 'demoLogin' | 'verify2fa' | 'me' | 'setup' | 'confirm' | 'disable' | 'revoke' | 'revokeAll' | 'refresh'>>;
  let service: AuthService;

  beforeEach(() => {
    localStorage.clear();
    client = {
      login: jest.fn(),
      demoLogin: jest.fn(),
      verify2fa: jest.fn(),
      me: jest.fn(),
      setup: jest.fn(),
      confirm: jest.fn(),
      disable: jest.fn(),
      revoke: jest.fn(),
      revokeAll: jest.fn(),
      refresh: jest.fn()
    };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: Client, useValue: client },
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    service = TestBed.inject(AuthService);
  });

  it("login() 2FA kapalıyken token'ları uygular ve requiresTwoFactor:false döner", async () => {
    const token = fakeJwt({ email: 'user@example.com', [ROLE_CLAIM_KEY]: 'BankAdmin' });
    client.login.mockReturnValue(of(new LoginResult({
      accessToken: token,
      refreshToken: 'raw-refresh-token',
      twoFactorRequired: false
    })));

    const outcome = await service.login('user@example.com', 'sifre');

    expect(outcome).toEqual({ requiresTwoFactor: false });
    expect(service.isAuthenticated()).toBe(true);
    expect(service.email()).toBe('user@example.com');
    expect(service.role()).toBe('BankAdmin');
    expect(localStorage.getItem('finara_refresh_token')).toBe('raw-refresh-token');
  });

  it("login() 2FA açıkken token uygulamaz, meydan okuma token'ını döner", async () => {
    client.login.mockReturnValue(of(new LoginResult({
      twoFactorRequired: true,
      challengeToken: 'challenge-abc'
    })));

    const outcome = await service.login('user@example.com', 'sifre');

    expect(outcome).toEqual({ requiresTwoFactor: true, challengeToken: 'challenge-abc' });
    expect(service.isAuthenticated()).toBe(false);
  });

  it('demoLogin() gerçek bir login gibi token uygular', async () => {
    const token = fakeJwt({ email: 'demo@finara.dev', [ROLE_CLAIM_KEY]: 'Analyst' });
    client.demoLogin.mockReturnValue(of(new LoginResult({ accessToken: token, refreshToken: 'demo-refresh' })));

    await service.demoLogin();

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isDemoAccount()).toBe(true);
    expect(service.isAnalyst()).toBe(true);
    expect(service.canWrite()).toBe(false);
  });

  it('isDemoAccount() normal bir hesapta false döner', async () => {
    const token = fakeJwt({ email: 'gercek@finara.dev', [ROLE_CLAIM_KEY]: 'BankAdmin' });
    client.login.mockReturnValue(of(new LoginResult({ accessToken: token, refreshToken: 'r', twoFactorRequired: false })));

    await service.login('gercek@finara.dev', 'sifre');

    expect(service.isDemoAccount()).toBe(false);
  });

  it("logout() localStorage'ı temizler ve revoke() çağırır", async () => {
    localStorage.setItem('finara_refresh_token', 'eski-token');
    client.revoke.mockReturnValue(of(void 0));

    await service.logout();

    expect(localStorage.getItem('finara_refresh_token')).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(client.revoke).toHaveBeenCalledWith(expect.objectContaining({ refreshToken: 'eski-token' }));
  });

  it('logout() refresh token yoksa revoke() çağırmaz', async () => {
    await service.logout();

    expect(client.revoke).not.toHaveBeenCalled();
  });

  it('logoutAllDevices() revokeAll() çağırır ve oturumu temizler', async () => {
    const token = fakeJwt({ email: 'user@example.com', [ROLE_CLAIM_KEY]: 'BankAdmin' });
    client.login.mockReturnValue(of(new LoginResult({ accessToken: token, refreshToken: 'r', twoFactorRequired: false })));
    await service.login('user@example.com', 'sifre');
    client.revokeAll.mockReturnValue(of(void 0));

    await service.logoutAllDevices();

    expect(client.revokeAll).toHaveBeenCalled();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('ensureFreshAccessToken() refresh token yoksa null döner ve refresh() çağırmaz', async () => {
    const result = await service.ensureFreshAccessToken();

    expect(result).toBeNull();
    expect(client.refresh).not.toHaveBeenCalled();
  });

  it('ensureFreshAccessToken() başarısız olursa oturumu temizler', async () => {
    localStorage.setItem('finara_refresh_token', 'suresi-dolmus');
    client.refresh.mockImplementation(() => {
      throw new Error('401');
    });

    await expect(service.ensureFreshAccessToken()).rejects.toThrow();
    expect(localStorage.getItem('finara_refresh_token')).toBeNull();
  });

  it('ensureFreshAccessToken() geçerli bir refresh token varsa yeni token uygular', async () => {
    localStorage.setItem('finara_refresh_token', 'eski-refresh');
    const newToken = fakeJwt({ email: 'user@example.com', [ROLE_CLAIM_KEY]: 'BankAdmin' });
    client.refresh.mockReturnValue(of(new AuthResult({ accessToken: newToken, refreshToken: 'yeni-refresh' })));

    const result = await service.ensureFreshAccessToken();

    expect(result).toBe(newToken);
    expect(localStorage.getItem('finara_refresh_token')).toBe('yeni-refresh');
  });
});
