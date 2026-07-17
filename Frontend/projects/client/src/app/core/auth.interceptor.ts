import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// /api/auth/* uçları (login/register/refresh/revoke) bilerek hariç tutuluyor —
// bunlar için henüz bir access token olmayabilir, olsa bile gerekmiyor.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const isAuthEndpoint = req.url.includes('/api/auth/');
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
