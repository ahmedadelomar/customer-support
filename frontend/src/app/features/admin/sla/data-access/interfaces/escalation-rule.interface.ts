import type { ConditionRow } from './sla-policy.interface';

/** Mirrors `EscalationTrigger`. */
export type EscalationTrigger = 0 | 1 | 2 | 3 | 4;
export const ESCALATION_TRIGGERS: { value: EscalationTrigger; labelKey: string; thresholdKind: 'percent' | 'minutes' | 'count' | 'none' }[] = [
  { value: 0, labelKey: 'sla.escalationRules.triggerOption.approachingBreach', thresholdKind: 'percent' },
  { value: 1, labelKey: 'sla.escalationRules.triggerOption.breached', thresholdKind: 'none' },
  { value: 2, labelKey: 'sla.escalationRules.triggerOption.noAgentResponse', thresholdKind: 'minutes' },
  { value: 3, labelKey: 'sla.escalationRules.triggerOption.customerReplyCount', thresholdKind: 'count' },
  { value: 4, labelKey: 'sla.escalationRules.triggerOption.reopenCount', thresholdKind: 'count' },
];

/** Mirrors `EscalationActionType`. */
export type EscalationActionType = 0 | 1 | 2 | 3 | 4 | 5;
export const ESCALATION_ACTIONS: { value: EscalationActionType; labelKey: string; target: 'user-or-team' | 'department' | 'user' | 'none' }[] = [
  { value: 0, labelKey: 'sla.escalationRules.actionOption.notifyManager', target: 'none' },
  { value: 1, labelKey: 'sla.escalationRules.actionOption.reassign', target: 'user-or-team' },
  { value: 2, labelKey: 'sla.escalationRules.actionOption.raisePriority', target: 'none' },
  { value: 3, labelKey: 'sla.escalationRules.actionOption.changeDepartment', target: 'department' },
  { value: 4, labelKey: 'sla.escalationRules.actionOption.increaseEscalationLevel', target: 'none' },
  { value: 5, labelKey: 'sla.escalationRules.actionOption.addWatcher', target: 'user' },
];

/** Mirrors `SlaTargetType`, or null for either. */
export type SlaTargetTypeFilter = 0 | 1 | null;

export interface EscalationRule {
  id: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
  evaluationOrder: number;
  isActive: boolean;
  trigger: EscalationTrigger;
  thresholdPercent: number | null;
  thresholdMinutes: number | null;
  thresholdCount: number | null;
  targetType: SlaTargetTypeFilter;
  conditions: ConditionRow[];
  action: EscalationActionType;
  actionTargetUserId: string | null;
  actionTargetTeamId: string | null;
  actionTargetDepartmentId: string | null;
  actionTargetPriorityId: string | null;
  actionNotifyRoleId: string | null;
  cooldownMinutes: number;
  maxFiresPerTicket: number;
  fireCount: number;
  lastFiredAt: string | null;
}

export interface EscalationRuleRequest {
  nameEn: string;
  nameAr: string;
  description: string | null;
  evaluationOrder: number;
  isActive?: boolean;
  trigger: EscalationTrigger;
  thresholdPercent: number | null;
  thresholdMinutes: number | null;
  thresholdCount: number | null;
  targetType: SlaTargetTypeFilter;
  conditions: ConditionRow[];
  action: EscalationActionType;
  actionTargetUserId: string | null;
  actionTargetTeamId: string | null;
  actionTargetDepartmentId: string | null;
  actionTargetPriorityId: string | null;
  actionNotifyRoleId: string | null;
  cooldownMinutes: number;
  maxFiresPerTicket: number;
}

export interface EscalationTestMatch {
  ticketId: string;
  ticketNumber: string;
  reason: string;
}
