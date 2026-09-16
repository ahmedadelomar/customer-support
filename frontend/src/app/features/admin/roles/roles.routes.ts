import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** Role and permission administration routes (Security & Administration / Permissions). */
export const ROLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/role-list/role-list.page').then((m) => m.RoleListPage),
    data: { titleKey: 'admin.roles.title' },
  },
  {
    path: ':id/permissions',
    canActivate: [permissionGuard],
    data: { titleKey: 'admin.roles.permissionsTitle', permissions: [PERMISSIONS.administration.manageRoles] },
    loadComponent: () =>
      import('./pages/role-permissions/role-permissions.page').then((m) => m.RolePermissionsPage),
  },
];
