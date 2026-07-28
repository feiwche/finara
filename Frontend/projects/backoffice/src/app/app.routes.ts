import { Routes } from '@angular/router';
import { authGuard, superAdminGuard } from '@finara/shared-ui';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    data: { titleKey: 'pageTitle.login' },
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'forgot-password',
    data: { titleKey: 'pageTitle.forgotPassword' },
    loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    data: { titleKey: 'pageTitle.resetPassword' },
    loadComponent: () => import('./features/auth/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'accept-invitation',
    data: { titleKey: 'pageTitle.acceptInvitation' },
    loadComponent: () => import('./features/auth/accept-invitation.component').then(m => m.AcceptInvitationComponent)
  },
  {
    path: 'guvenlik',
    data: { titleKey: 'pageTitle.security' },
    loadComponent: () => import('./features/security/security-page.component').then(m => m.SecurityPageComponent)
  },
  {
    path: 'nasil-calisir',
    data: { titleKey: 'pageTitle.scenarios' },
    loadComponent: () => import('./features/scenarios/scenarios-page.component').then(m => m.ScenariosPageComponent)
  },
  {
    path: '',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        data: { titleKey: 'pageTitle.dashboard' },
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'campaigns',
        data: { titleKey: 'pageTitle.campaigns' },
        loadComponent: () => import('./features/campaigns/campaign-management.component').then(m => m.CampaignManagementComponent)
      },
      {
        path: 'audit-logs',
        data: { titleKey: 'pageTitle.auditLogs' },
        loadComponent: () => import('./features/audit-logs/audit-log.component').then(m => m.AuditLogComponent)
      },
      {
        path: 'admin-accounts',
        data: { titleKey: 'pageTitle.adminAccounts' },
        loadComponent: () => import('./features/admin-accounts/admin-accounts.component').then(m => m.AdminAccountsComponent)
      },
      {
        path: 'banks',
        data: { titleKey: 'pageTitle.banks' },
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/banks/bank-management.component').then(m => m.BankManagementComponent)
      },
      {
        path: 'cohorts',
        data: { titleKey: 'pageTitle.cohorts' },
        loadComponent: () => import('./features/cohorts/cohort-explorer.component').then(m => m.CohortExplorerComponent)
      },
      {
        path: 'notifications',
        data: { titleKey: 'pageTitle.notifications' },
        loadComponent: () => import('./features/notifications/notification-log.component').then(m => m.NotificationLogComponent)
      },
      {
        path: 'hesabim',
        data: { titleKey: 'pageTitle.account' },
        loadComponent: () => import('./features/account/account-page.component').then(m => m.AccountPageComponent)
      },
      {
        path: 'ayarlar',
        data: { titleKey: 'pageTitle.settings' },
        loadComponent: () => import('./features/settings/settings-page.component').then(m => m.SettingsPageComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
