import { Routes } from '@angular/router';

/** System configuration routes (Security & Administration / System configuration). */
export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/settings/settings.page').then((m) => m.SettingsPage),
    data: { titleKey: 'admin.settings.title' },
  },
];
