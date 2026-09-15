import { Routes } from '@angular/router';

/** Team collaboration routes (Agent Dashboard / Team collaboration). Follows the shape established by `quick-replies.routes.ts`. */
export const COLLABORATION_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/mentions-inbox/mentions-inbox.page').then((m) => m.MentionsInboxPage),
    data: { titleKey: 'collaboration.mentions.title' },
  },
];
