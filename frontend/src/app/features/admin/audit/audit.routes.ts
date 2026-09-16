import { Routes } from '@angular/router';

/** Audit trail routes (Security & Administration / Audit logs). Read-only: no create or edit route exists. */
export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/audit-log/audit-log.page').then((m) => m.AuditLogPage),
    data: { titleKey: 'admin.audit.title' },
  },
];
