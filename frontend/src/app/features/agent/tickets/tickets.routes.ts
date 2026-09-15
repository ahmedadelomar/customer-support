import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/**
 * Ticket routes (Ticket Management / Create and track tickets). Follows the shape established by
 * `customers.routes.ts`.
 */
export const TICKET_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/ticket-list/ticket-list.page').then((m) => m.TicketListPage),
    data: { titleKey: 'tickets.title' },
  },
  {
    path: 'new',
    canActivate: [permissionGuard],
    data: { titleKey: 'tickets.create', permissions: [PERMISSIONS.tickets.create] },
    loadComponent: () => import('./pages/ticket-form/ticket-form.page').then((m) => m.TicketFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/ticket-detail/ticket-detail.page').then((m) => m.TicketDetailPage),
    data: { titleKey: 'tickets.detail' },
  },
];
