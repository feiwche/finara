import { Routes } from '@angular/router';
import { authGuard, superAdminGuard } from '@finara/shared-ui';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Finara BackOffice — Giriş',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'forgot-password',
    title: 'Finara BackOffice — Şifremi Unuttum',
    loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    title: 'Finara BackOffice — Şifre Sıfırla',
    loadComponent: () => import('./features/auth/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'accept-invitation',
    title: 'Finara BackOffice — Daveti Kabul Et',
    loadComponent: () => import('./features/auth/accept-invitation.component').then(m => m.AcceptInvitationComponent)
  },
  {
    path: 'guvenlik',
    title: 'Finara BackOffice — Güvenlik ve Veri Gizliliği',
    loadComponent: () => import('./features/security/security-page.component').then(m => m.SecurityPageComponent)
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        title: 'Finara BackOffice — Genel Bakış',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
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
      },
      {
        path: 'banks',
        title: 'Finara BackOffice — Banka Yönetimi',
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/banks/bank-management.component').then(m => m.BankManagementComponent)
      },
      {
        path: 'cohorts',
        title: 'Finara BackOffice — Kohort Gezgini',
        loadComponent: () => import('./features/cohorts/cohort-explorer.component').then(m => m.CohortExplorerComponent)
      },
      {
        path: 'notifications',
        title: 'Finara BackOffice — Bildirim Kaydı',
        loadComponent: () => import('./features/notifications/notification-log.component').then(m => m.NotificationLogComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
