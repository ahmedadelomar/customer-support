import { Routes } from '@angular/router';

/** Status workflow administration routes (Ticket Management / Status workflow and escalation). */
export const STATUS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/status-workflow/status-workflow.page').then((m) => m.StatusWorkflowPage),
    data: { titleKey: 'admin.statuses.title' },
  },
];
