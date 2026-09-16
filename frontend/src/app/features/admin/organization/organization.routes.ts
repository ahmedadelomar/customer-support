import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** Department and team administration (Platform / Departments, teams and queue scoping). */
export const ORGANIZATION_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'departments' },
  {
    path: 'departments',
    canActivate: [permissionGuard],
    data: { titleKey: 'admin.departments.title', permissions: [PERMISSIONS.tickets.view] },
    loadComponent: () =>
      import('./pages/department-list/department-list.page').then((m) => m.DepartmentListPage),
  },
  {
    path: 'teams',
    canActivate: [permissionGuard],
    data: { titleKey: 'admin.teams.title', permissions: [PERMISSIONS.administration.manageTeams] },
    loadComponent: () => import('./pages/team-list/team-list.page').then((m) => m.TeamListPage),
  },
];
