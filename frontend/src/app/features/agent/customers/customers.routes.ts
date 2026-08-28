import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

export const CUSTOMER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./customer-list.page').then((m) => m.CustomerListPage),
    data: { titleKey: 'customers.title' },
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { titleKey: 'customers.create', permissions: [PERMISSIONS.customers.create] },
    loadComponent: () => import('./customer-form.page').then((m) => m.CustomerFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./customer-detail.page').then((m) => m.CustomerDetailPage),
    data: { titleKey: 'customers.detail' },
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard],
    data: { titleKey: 'customers.edit', permissions: [PERMISSIONS.customers.update] },
    loadComponent: () => import('./customer-form.page').then((m) => m.CustomerFormPage),
  },
];
