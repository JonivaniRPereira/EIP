import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth-guard';
import { tenantGuard } from './core/auth/tenant-guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: 'select-tenant',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/select-tenant/select-tenant').then((m) => m.SelectTenant),
  },
  {
    path: 'create-tenant',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/create-tenant/create-tenant').then((m) => m.CreateTenant),
  },
  {
    // App shell (sidebar + topbar, estilo Able Pro) envolve toda rota autenticada+com-tenant — o
    // guard vale para todas as rotas filhas de uma vez, não precisa repetir em cada uma.
    path: '',
    canActivate: [tenantGuard],
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    children: [
      {
        path: 'connectors',
        loadComponent: () => import('./features/connectors/connectors').then((m) => m.Connectors),
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'dashboards',
        loadComponent: () =>
          import('./features/dashboards/dashboard-list/dashboard-list').then(
            (m) => m.DashboardList,
          ),
      },
      {
        path: 'dashboards/new',
        loadComponent: () =>
          import('./features/dashboards/dashboard-form/dashboard-form').then(
            (m) => m.DashboardForm,
          ),
      },
      {
        path: 'dashboards/:id',
        loadComponent: () =>
          import('./features/dashboards/dashboard-viewer/dashboard-viewer').then(
            (m) => m.DashboardViewer,
          ),
      },
      {
        path: 'dashboards/:id/edit',
        loadComponent: () =>
          import('./features/dashboards/dashboard-form/dashboard-form').then(
            (m) => m.DashboardForm,
          ),
      },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
