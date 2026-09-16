/** Mirrors `ConditionOperator` — the shared allow-listed condition shape used by policies, assignment and escalation rules. */
export type ConditionOperator =
  | 'Equals'
  | 'NotEquals'
  | 'In'
  | 'NotIn'
  | 'Contains'
  | 'GreaterThan'
  | 'LessThan'
  | 'IsNull'
  | 'IsNotNull';

export interface ConditionRow {
  field: string;
  operator: ConditionOperator;
  value: string | null;
}

export interface SlaTarget {
  priorityId: string;
  priorityCode: string;
  priorityNameEn: string;
  priorityNameAr: string;
  firstResponseMinutes: number;
  resolutionMinutes: number;
}

export interface SlaPolicy {
  id: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
  businessCalendarId: string;
  calendarNameEn: string;
  calendarNameAr: string;
  evaluationOrder: number;
  isDefault: boolean;
  isActive: boolean;
  warningThresholdPercent: number;
  pauseOnPendingCustomer: boolean;
  targets: SlaTarget[];
  conditions: ConditionRow[];
}

export interface SlaPolicyRequest {
  nameEn: string;
  nameAr: string;
  description: string | null;
  businessCalendarId: string;
  evaluationOrder: number;
  isDefault: boolean;
  isActive?: boolean;
  warningThresholdPercent: number;
  pauseOnPendingCustomer: boolean;
  targets: { priorityId: string; firstResponseMinutes: number; resolutionMinutes: number }[];
  conditions: ConditionRow[];
}

export interface SlaPolicyPreviewResult {
  firstResponseDueAt: string | null;
  resolutionDueAt: string | null;
  note: string | null;
}
