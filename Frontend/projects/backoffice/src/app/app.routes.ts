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
        path: 'cohorts',
        title: 'Finara BackOffice — Kohort Gezgini',
        loadComponent: () => import('./features/cohorts/cohort-explorer.component').then(m => m.CohortExplorerComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
