import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) return true;

  return router.createUrlTree(['/login']);
};

// Sadece nav linkinde gizlemek yetmiyor — BankAdmin doğrudan URL ile erişip
// backend'de de aynı şekilde korunmayan bir uca (bkz. BankService.GetBanksAsync
// düzeltmesi) ulaşabiliyordu (kanıtlandı, güvenlik incelemesinde bulundu). Bu guard
// authGuard'ın ÜZERİNE eklenir (route zaten authGuard'dan geçmiş olmalı), sadece
// SuperAdmin-only ekranlar için ikinci bir savunma katmanı.
export const superAdminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isSuperAdmin()) return true;

  return router.createUrlTree(['/dashboard']);
};
