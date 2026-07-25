import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Bu uçlar bilerek hariç tutuluyor — çağrıldıklarında henüz bir access token
// olmayabilir, olsa bile gerekmiyor (kimlik doğrulama kendi içinde farklı bir
// kanıtla — parola, refresh token, davet/sıfırlama token'ı — yapılıyor).
// "/api/auth/revoke-all" bilinçli olarak bu listede DEĞİL — o kendi hesabını JWT'den
// (Authorization header) bulduğu için tam tersine bu interceptor'ın eklediği token'a
// ihtiyaç duyuyor.
const PUBLIC_AUTH_PATHS = [
  '/api/auth/register',
  '/api/auth/login',
  '/api/auth/refresh',
  '/api/auth/revoke',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/api/auth/invitations',
  '/api/auth/accept-invitation'
];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requestPath = req.url.split('?')[0];
  const isAuthEndpoint = PUBLIC_AUTH_PATHS.some(path => requestPath.endsWith(path));
  const token = authService.accessToken;

  const authedReq = !token || isAuthEndpoint
    ? req
    : req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      // 401 alan auth-dışı bir istek varsa, tek seferlik refresh dene ve isteği tekrarla —
      // access token yalnızca bellekte olduğu için (sayfa yenilenince kaybolur), 401'in
      // "gerçekten süresi dolmuş" mu yoksa sadece henüz geri yüklenmemiş mi olduğunu
      // ayırt etmenin yolu bu.
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      return from(authService.ensureFreshAccessToken()).pipe(
        switchMap((newToken) => {
          if (!newToken) {
            router.navigate(['/login']);
            return throwError(() => error);
          }
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }));
        }),
        catchError((refreshError: unknown) => {
          router.navigate(['/login']);
          return throwError(() => refreshError);
        })
      );
    })
  );
};
