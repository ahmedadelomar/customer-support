import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** Web form administration (Communication Channels / Web forms). */
export const WEB_FORMS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [permissionGuard],
    data: { titleKey: 'webForms.admin.title', permissions: [PERMISSIONS.channels.manageWebForms] },
    loadComponent: () => import('./pages/web-form-list/web-form-list.page').then((m) => m.WebFormListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { titleKey: 'webForms.admin.newTitle', permissions: [PERMISSIONS.channels.manageWebForms] },
    loadComponent: () => import('./pages/web-form-form/web-form-form.page').then((m) => m.WebFormFormPage),
  },
  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { titleKey: 'webForms.admin.editTitle', permissions: [PERMISSIONS.channels.manageWebForms] },
    loadComponent: () => import('./pages/web-form-form/web-form-form.page').then((m) => m.WebFormFormPage),
  },
  {
    path: ':id/submissions',
    canActivate: [permissionGuard],
    data: { titleKey: 'webForms.admin.submissionsTitle', permissions: [PERMISSIONS.channels.manageWebForms] },
    loadComponent: () => import('./pages/web-form-submissions/web-form-submissions.page').then((m) => m.WebFormSubmissionsPage),
  },
];
