import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jest.Mocked<Pick<AuthService, 'ensureFreshAccessToken'>> & { accessToken: string | null };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = { accessToken: null, ensureFreshAccessToken: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('PUBLIC_AUTH_PATHS içindeki bir uca (login) token varken bile Authorization eklemez', () => {
    authService.accessToken = 'gecerli-token';

    httpClient.post('http://localhost:5068/api/auth/login', {}).subscribe();

    const req = httpMock.expectOne('http://localhost:5068/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('public olmayan bir uca token varsa Authorization header ekler', () => {
    authService.accessToken = 'gecerli-token';

    httpClient.get('http://localhost:5068/api/customers').subscribe();

    const req = httpMock.expectOne('http://localhost:5068/api/customers');
    expect(req.request.headers.get('Authorization')).toBe('Bearer gecerli-token');
    req.flush({});
  });

  it('token yokken hiçbir uca Authorization eklemez', () => {
    authService.accessToken = null;

    httpClient.get('http://localhost:5068/api/customers').subscribe();

    const req = httpMock.expectOne('http://localhost:5068/api/customers');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('demo-login gibi query-string\'li public uçlarda da Authorization eklemez', () => {
    authService.accessToken = 'gecerli-token';

    httpClient.get('http://localhost:5068/api/auth/invitations?token=abc').subscribe();

    const req = httpMock.expectOne('http://localhost:5068/api/auth/invitations?token=abc');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('public olmayan bir uçta 401 alınca ensureFreshAccessToken çağırıp isteği yeni token ile tekrarlar', () => {
    authService.accessToken = 'eski-token';
    authService.ensureFreshAccessToken.mockResolvedValue('yeni-token');

    let result: unknown;
    httpClient.get('http://localhost:5068/api/customers').subscribe({ next: r => (result = r) });

    const firstReq = httpMock.expectOne('http://localhost:5068/api/customers');
    firstReq.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    return Promise.resolve().then(() => {
      const retryReq = httpMock.expectOne('http://localhost:5068/api/customers');
      expect(retryReq.request.headers.get('Authorization')).toBe('Bearer yeni-token');
      retryReq.flush({ ok: true });
      expect(result).toEqual({ ok: true });
    });
  });

  it('yenileme de başarısız olursa /login\'e yönlendirir', () => {
    authService.accessToken = 'eski-token';
    authService.ensureFreshAccessToken.mockResolvedValue(null);

    httpClient.get('http://localhost:5068/api/customers').subscribe({ error: () => undefined });

    const firstReq = httpMock.expectOne('http://localhost:5068/api/customers');
    firstReq.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    return Promise.resolve().then(() => {
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  it('login uç noktasında 401 alınca yenileme denemez (kendisi zaten bir auth ucu)', () => {
    authService.accessToken = null;

    httpClient.post('http://localhost:5068/api/auth/login', {}).subscribe({ error: () => undefined });

    const req = httpMock.expectOne('http://localhost:5068/api/auth/login');
    req.flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(authService.ensureFreshAccessToken).not.toHaveBeenCalled();
  });
});
