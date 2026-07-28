import { Routes } from '@angular/router';
import { authGuard } from '@finara/shared-ui';
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
      { path: '', redirectTo: 'customers', pathMatch: 'full' },
      {
        path: 'customers',
        data: { titleKey: 'pageTitle.customers' },
        loadComponent: () => import('./features/customers/customer-list.component').then(m => m.CustomerListComponent)
      },
      {
        // Müşteri adı yüklendiğinde CustomerDetailComponent bu başlığı Title servisiyle günceller.
        path: 'customers/:id',
        data: { titleKey: 'pageTitle.customerDetail' },
        loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent)
      },
      {
        path: 'campaigns',
        data: { titleKey: 'pageTitle.campaigns' },
        loadComponent: () => import('./features/campaigns/campaign-list.component').then(m => m.CampaignListComponent)
      },
      {
        path: 'tools',
        data: { titleKey: 'pageTitle.tools' },
        loadComponent: () => import('./features/tools/tools.component').then(m => m.ToolsComponent)
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
