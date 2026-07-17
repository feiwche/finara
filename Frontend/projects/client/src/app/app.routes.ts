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
