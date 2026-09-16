import { Routes } from '@angular/router';

/** Live chat agent console (Communication Channels / Live chat). Permission is already checked by the parent `/agent/chat` route. */
export const CHAT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/chat-console/chat-console.page').then((m) => m.ChatConsolePage),
  },
];
