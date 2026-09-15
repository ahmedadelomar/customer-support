import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/auth/auth.guards';
import { PERMISSIONS } from './core/permissions';

/**
 * Three top-level areas, each lazily loaded:
 *  - `/agent`  the staff workspace
 *  - `/portal` the customer self-service portal
 *  - `/admin`  configuration and security
 *
 * Every feature route declares the permissions it needs in `data.permissions`, checked by
 * `permissionGuard`. The same keys gate the corresponding API endpoints server-side.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'agent/dashboard' },

  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },

  {
    path: 'agent',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/agent-shell/agent-shell.page').then((m) => m.AgentShellPage),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/agent/dashboard/pages/dashboard/dashboard.page').then((m) => m.DashboardPage),
        data: { titleKey: 'nav.dashboard' },
      },
      {
        path: 'customers',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.customers', permissions: [PERMISSIONS.customers.view] },
        loadChildren: () =>
          import('./features/agent/customers/customers.routes').then((m) => m.CUSTOMER_ROUTES),
      },
      {
        path: 'tickets',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.tickets', permissions: [PERMISSIONS.tickets.view] },
        loadChildren: () =>
          import('./features/agent/tickets/tickets.routes').then((m) => m.TICKET_ROUTES),
      },
      {
        path: 'tasks',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.tasks', permissions: [PERMISSIONS.workspace.viewOwnTasks] },
        loadChildren: () =>
          import('./features/agent/tasks/tasks.routes').then((m) => m.TASK_ROUTES),
      },
      {
        path: 'quick-replies',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.quickReplies', permissions: [PERMISSIONS.workspace.manageQuickReplies] },
        loadChildren: () =>
          import('./features/agent/quick-replies/quick-replies.routes').then((m) => m.QUICK_REPLY_ROUTES),
      },
    ],
  },

  {
    path: 'admin',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/admin-shell/admin-shell.page').then((m) => m.AdminShellPage),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'ticket-categories' },
      {
        path: 'ticket-categories',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.ticketCategories', permissions: [PERMISSIONS.tickets.manageCategories] },
        loadChildren: () =>
          import('./features/admin/categories/categories.routes').then((m) => m.CATEGORY_ROUTES),
      },
      {
        path: 'ticket-priorities',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.ticketPriorities', permissions: [PERMISSIONS.tickets.managePriorities] },
        loadChildren: () =>
          import('./features/admin/priorities/priorities.routes').then((m) => m.PRIORITY_ROUTES),
      },
      {
        path: 'ticket-statuses',
        canActivate: [permissionGuard],
        data: { titleKey: 'nav.ticketStatuses', permissions: [PERMISSIONS.tickets.manageStatuses] },
        loadChildren: () =>
          import('./features/admin/statuses/statuses.routes').then((m) => m.STATUS_ROUTES),
      },
    ],
  },

  {
    path: 'forbidden',
    loadComponent: () => import('./features/errors/forbidden.page').then((m) => m.ForbiddenPage),
  },
  {
    path: '**',
    loadComponent: () => import('./features/errors/not-found.page').then((m) => m.NotFoundPage),
  },
];
