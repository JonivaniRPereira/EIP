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
    loadComponent: () => import('./features/select-tenant/select-tenant').then((m) => m.SelectTenant),
  },
  {
    path: 'dashboard',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'dashboards',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/dashboards/dashboard-list/dashboard-list').then((m) => m.DashboardList),
  },
  {
    path: 'dashboards/new',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/dashboards/dashboard-form/dashboard-form').then((m) => m.DashboardForm),
  },
  {
    path: 'dashboards/:id',
    canActivate: [tenantGuard],
    loadComponent: () =>
      import('./features/dashboards/dashboard-viewer/dashboard-viewer').then((m) => m.DashboardViewer),
  },
  {
    path: 'dashboards/:id/edit',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/dashboards/dashboard-form/dashboard-form').then((m) => m.DashboardForm),
  },
  { path: '**', redirectTo: 'login' },
];
