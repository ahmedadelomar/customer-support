import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** Channel account administration (Communication Channels / Email channel and beyond). */
export const CHANNELS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [permissionGuard],
    data: { titleKey: 'channels.title', permissions: [PERMISSIONS.channels.manage] },
    loadComponent: () =>
      import('./pages/channel-account-list/channel-account-list.page').then((m) => m.ChannelAccountListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { titleKey: 'channels.newTitle', permissions: [PERMISSIONS.channels.manage] },
    loadComponent: () =>
      import('./pages/channel-account-form/channel-account-form.page').then((m) => m.ChannelAccountFormPage),
  },
  {
    path: ':id',
    canActivate: [permissionGuard],
    data: { titleKey: 'channels.editTitle', permissions: [PERMISSIONS.channels.manage] },
    loadComponent: () =>
      import('./pages/channel-account-form/channel-account-form.page').then((m) => m.ChannelAccountFormPage),
  },
];
