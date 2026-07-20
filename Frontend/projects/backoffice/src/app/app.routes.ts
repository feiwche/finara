import { Routes } from '@angular/router';
import { authGuard } from '@finara/shared-ui';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Finara BackOffice — Giriş',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'campaigns', pathMatch: 'full' },
      {
        path: 'campaigns',
        title: 'Finara BackOffice — Kampanya Yönetimi',
        loadComponent: () => import('./features/campaigns/campaign-management.component').then(m => m.CampaignManagementComponent)
      },
      {
        path: 'audit-logs',
        title: 'Finara BackOffice — Denetim Kaydı',
        loadComponent: () => import('./features/audit-logs/audit-log.component').then(m => m.AuditLogComponent)
      },
      {
        path: 'admin-accounts',
        title: 'Finara BackOffice — Yönetici Hesapları',
        loadComponent: () => import('./features/admin-accounts/admin-accounts.component').then(m => m.AdminAccountsComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
