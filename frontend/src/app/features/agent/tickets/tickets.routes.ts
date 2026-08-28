import { Routes } from '@angular/router';

/**
 * Ticket routes. The list and detail pages land here from the Ticket Management stories;
 * the file exists now so `app.routes.ts` resolves and the navigation entry is reachable.
 */
export const TICKET_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('../../errors/not-found.page').then((m) => m.NotFoundPage),
    data: { titleKey: 'tickets.title' },
  },
];
