import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** User administration routes (Security & Administration / Users and roles). */
export const USER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/user-list/user-list.page').then((m) => m.UserListPage),
    data: { titleKey: 'admin.users.title' },
  },
  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { titleKey: 'admin.users.edit', permissions: [PERMISSIONS.administration.manageUsers] },
    loadComponent: () => import('./pages/user-form/user-form.page').then((m) => m.UserFormPage),
  },
];
