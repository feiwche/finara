import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { AppShellComponent } from './layout/app-shell.component';

export const routes: Routes = [
  {
    path: 'login',
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
        loadComponent: () => import('./features/customers/customer-list.component').then(m => m.CustomerListComponent)
      },
      {
        path: 'customers/:id',
        loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent)
      },
      {
        path: 'campaigns',
        loadComponent: () => import('./features/campaigns/campaign-list.component').then(m => m.CampaignListComponent)
      },
      {
        path: 'tools',
        loadComponent: () => import('./features/tools/tools.component').then(m => m.ToolsComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
