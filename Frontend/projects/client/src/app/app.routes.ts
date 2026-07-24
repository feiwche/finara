import { Routes } from '@angular/router';
import { authGuard } from '@finara/shared-ui';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Finara — Giriş',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'forgot-password',
    title: 'Finara — Şifremi Unuttum',
    loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    title: 'Finara — Şifre Sıfırla',
    loadComponent: () => import('./features/auth/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'guvenlik',
    title: 'Finara — Güvenlik ve Veri Gizliliği',
    loadComponent: () => import('./features/security/security-page.component').then(m => m.SecurityPageComponent)
  },
  {
    path: 'nasil-calisir',
    title: 'Finara — Nasıl Çalışır',
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
        title: 'Finara — Müşteriler',
        loadComponent: () => import('./features/customers/customer-list.component').then(m => m.CustomerListComponent)
      },
      {
        // Müşteri adı yüklendiğinde CustomerDetailComponent bu başlığı Title servisiyle günceller.
        path: 'customers/:id',
        title: 'Finara — Müşteri Detayı',
        loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent)
      },
      {
        path: 'campaigns',
        title: 'Finara — Kampanyalar',
        loadComponent: () => import('./features/campaigns/campaign-list.component').then(m => m.CampaignListComponent)
      },
      {
        path: 'tools',
        title: 'Finara — Araçlar',
        loadComponent: () => import('./features/tools/tools.component').then(m => m.ToolsComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
