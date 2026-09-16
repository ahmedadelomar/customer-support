import { Routes } from '@angular/router';

/** Runtime branding administration (Platform / Runtime branding and theming). */
export const BRANDING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/branding/branding.page').then((m) => m.BrandingPage),
    data: { titleKey: 'admin.branding.title' },
  },
];
