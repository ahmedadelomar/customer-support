import { Routes } from '@angular/router';

/** Priority scale administration routes (Ticket Management / Categories and priorities). */
export const PRIORITY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/priority-scale/priority-scale.page').then((m) => m.PriorityScalePage),
    data: { titleKey: 'admin.priorities.title' },
  },
];
