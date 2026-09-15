import { Routes } from '@angular/router';

/** Quick reply routes (Agent Dashboard / Quick replies). Follows the shape established by `tasks.routes.ts`. */
export const QUICK_REPLY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/quick-reply-list/quick-reply-list.page').then((m) => m.QuickReplyListPage),
    data: { titleKey: 'quickReplies.title' },
  },
];
