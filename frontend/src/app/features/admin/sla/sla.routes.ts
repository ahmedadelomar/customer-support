import { Routes } from '@angular/router';
import { permissionGuard } from '../../../core/auth/auth.guards';
import { PERMISSIONS } from '../../../core/permissions';

/** SLA and Automation administration: policies, calendars, assignment rules, escalation rules and the shared decision log. */
export const SLA_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'policies' },
  {
    path: 'policies',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.policies.title', permissions: [PERMISSIONS.sla.managePolicies] },
    loadComponent: () => import('./pages/policy-list/policy-list.page').then((m) => m.PolicyListPage),
  },
  {
    path: 'policies/new',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.policies.newTitle', permissions: [PERMISSIONS.sla.managePolicies] },
    loadComponent: () => import('./pages/policy-form/policy-form.page').then((m) => m.PolicyFormPage),
  },
  {
    path: 'policies/:id',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.policies.editTitle', permissions: [PERMISSIONS.sla.managePolicies] },
    loadComponent: () => import('./pages/policy-form/policy-form.page').then((m) => m.PolicyFormPage),
  },
  {
    path: 'calendars',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.calendars.title', permissions: [PERMISSIONS.sla.manageCalendars] },
    loadComponent: () => import('./pages/calendar-list/calendar-list.page').then((m) => m.CalendarListPage),
  },
  {
    path: 'calendars/new',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.calendars.newTitle', permissions: [PERMISSIONS.sla.manageCalendars] },
    loadComponent: () => import('./pages/calendar-form/calendar-form.page').then((m) => m.CalendarFormPage),
  },
  {
    path: 'calendars/:id',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.calendars.editTitle', permissions: [PERMISSIONS.sla.manageCalendars] },
    loadComponent: () => import('./pages/calendar-form/calendar-form.page').then((m) => m.CalendarFormPage),
  },
  {
    path: 'assignment-rules',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.assignmentRules.title', permissions: [PERMISSIONS.sla.manageAssignmentRules] },
    loadComponent: () =>
      import('./pages/assignment-rule-list/assignment-rule-list.page').then((m) => m.AssignmentRuleListPage),
  },
  {
    path: 'assignment-rules/new',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.assignmentRules.newTitle', permissions: [PERMISSIONS.sla.manageAssignmentRules] },
    loadComponent: () =>
      import('./pages/assignment-rule-form/assignment-rule-form.page').then((m) => m.AssignmentRuleFormPage),
  },
  {
    path: 'assignment-rules/:id',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.assignmentRules.editTitle', permissions: [PERMISSIONS.sla.manageAssignmentRules] },
    loadComponent: () =>
      import('./pages/assignment-rule-form/assignment-rule-form.page').then((m) => m.AssignmentRuleFormPage),
  },
  {
    path: 'escalation-rules',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.escalationRules.title', permissions: [PERMISSIONS.sla.manageEscalationRules] },
    loadComponent: () =>
      import('./pages/escalation-rule-list/escalation-rule-list.page').then((m) => m.EscalationRuleListPage),
  },
  {
    path: 'escalation-rules/new',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.escalationRules.newTitle', permissions: [PERMISSIONS.sla.manageEscalationRules] },
    loadComponent: () =>
      import('./pages/escalation-rule-form/escalation-rule-form.page').then((m) => m.EscalationRuleFormPage),
  },
  {
    path: 'escalation-rules/:id',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.escalationRules.editTitle', permissions: [PERMISSIONS.sla.manageEscalationRules] },
    loadComponent: () =>
      import('./pages/escalation-rule-form/escalation-rule-form.page').then((m) => m.EscalationRuleFormPage),
  },
  {
    path: 'log',
    canActivate: [permissionGuard],
    data: { titleKey: 'sla.log.title', permissions: [PERMISSIONS.sla.view] },
    loadComponent: () => import('./pages/decision-log/decision-log.page').then((m) => m.DecisionLogPage),
  },
];
