export interface AutomationRunLog {
  id: string;
  ruleType: 'AssignmentRule' | 'EscalationRule';
  ruleId: string;
  ruleName: string | null;
  ticketId: string;
  ticketNumber: string | null;
  outcome: 'Matched' | 'Skipped' | 'Failed';
  reason: string | null;
  error: string | null;
  durationMs: number;
  occurredAt: string;
}

export interface AutomationRunLogFilter {
  ticketId?: string;
  ruleId?: string;
  ruleType?: string;
  outcome?: string;
  take?: number;
}
