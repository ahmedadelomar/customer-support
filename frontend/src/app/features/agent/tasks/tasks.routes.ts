import { Routes } from '@angular/router';

/** Task routes (Agent Dashboard / Tasks and reminders). Follows the shape established by `tickets.routes.ts`. */
export const TASK_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/task-list/task-list.page').then((m) => m.TaskListPage),
    data: { titleKey: 'tasks.title' },
  },
];
