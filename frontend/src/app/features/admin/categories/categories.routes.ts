import { Routes } from '@angular/router';

/** Category administration routes (Ticket Management / Categories and priorities). */
export const CATEGORY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/category-tree/category-tree.page').then((m) => m.CategoryTreePage),
    data: { titleKey: 'admin.categories.title' },
  },
];
