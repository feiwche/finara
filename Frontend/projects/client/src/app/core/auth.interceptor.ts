import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

// /api/auth/* uçları (login/register/refresh/revoke) bilerek hariç tutuluyor —
// bunlar için henüz bir access token olmayabilir, olsa bile gerekmiyor.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.accessToken;

  if (!token || req.url.includes('/api/auth/')) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
