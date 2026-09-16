import type { ConditionRow } from './sla-policy.interface';

/** Mirrors `AssignmentStrategy`. */
export type AssignmentStrategy = 0 | 1 | 2 | 3 | 4;
export const ASSIGNMENT_STRATEGIES: { value: AssignmentStrategy; labelKey: string; explainKey: string }[] = [
  { value: 0, labelKey: 'sla.assignmentRules.strategyOption.roundRobin', explainKey: 'sla.assignmentRules.strategyExplain.roundRobin' },
  { value: 1, labelKey: 'sla.assignmentRules.strategyOption.loadBalanced', explainKey: 'sla.assignmentRules.strategyExplain.loadBalanced' },
  { value: 2, labelKey: 'sla.assignmentRules.strategyOption.skillBased', explainKey: 'sla.assignmentRules.strategyExplain.skillBased' },
  { value: 3, labelKey: 'sla.assignmentRules.strategyOption.direct', explainKey: 'sla.assignmentRules.strategyExplain.direct' },
  { value: 4, labelKey: 'sla.assignmentRules.strategyOption.queueOnly', explainKey: 'sla.assignmentRules.strategyExplain.queueOnly' },
];

export interface AssignmentRule {
  id: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
  evaluationOrder: number;
  isActive: boolean;
  conditions: ConditionRow[];
  strategy: AssignmentStrategy;
  targetDepartmentId: string | null;
  targetTeamId: string | null;
  targetUserId: string | null;
  respectAgentAvailability: boolean;
  stopProcessing: boolean;
  matchCount: number;
  lastMatchedAt: string | null;
}

export interface AssignmentRuleRequest {
  nameEn: string;
  nameAr: string;
  description: string | null;
  evaluationOrder: number;
  isActive?: boolean;
  conditions: ConditionRow[];
  strategy: AssignmentStrategy;
  targetDepartmentId: string | null;
  targetTeamId: string | null;
  targetUserId: string | null;
  respectAgentAvailability: boolean;
  stopProcessing: boolean;
}

export interface AssignmentPreview {
  conditionsMatched: boolean;
  chosenAgentId: string | null;
  candidateCount: number;
  eligibleCount: number;
  reason: string;
}
