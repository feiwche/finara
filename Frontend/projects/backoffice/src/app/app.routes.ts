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
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
