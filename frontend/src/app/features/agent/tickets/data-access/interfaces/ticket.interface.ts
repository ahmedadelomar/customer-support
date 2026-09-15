import type { ChannelKey, TicketStatusKind } from '../../../../../core/models/enums';
import type { PagedQuery } from '../../../../../core/models/api.models';

export interface TicketListItem {
  id: string;
  number: string;
  subject: string;
  customerId: string;
  customerDisplayNameEn: string;
  customerDisplayNameAr: string;
  categoryId: string;
  categoryNameEn: string;
  categoryNameAr: string;
  priorityId: string;
  priorityNameEn: string;
  priorityNameAr: string;
  priorityColorHex: string;
  statusId: string;
  statusNameEn: string;
  statusNameAr: string;
  statusColorHex: string;
  statusKind: TicketStatusKind;
  channel: ChannelKey;
  departmentId: string | null;
  assignedAgentId: string | null;
  assignedAgentNameEn: string | null;
  assignedAgentNameAr: string | null;
  firstResponseDueAt: string | null;
  resolutionDueAt: string | null;
  isFirstResponseBreached: boolean;
  isResolutionBreached: boolean;
  lastCustomerReplyAt: string | null;
  lastAgentReplyAt: string | null;
  createdAt: string;
}

/** "mine" | "team" | "unassigned" | "all". */
export type TicketAssignmentFilter = 'mine' | 'team' | 'unassigned' | 'all';

export interface TicketQuery extends PagedQuery {
  assignment?: TicketAssignmentFilter;
  statusId?: string;
  statusKind?: TicketStatusKind;
  priorityId?: string;
  categoryId?: string;
  channel?: ChannelKey;
  departmentId?: string;
  customerId?: string;
  slaState?: 'breached' | 'duesoon' | 'ontrack';
  from?: string;
  to?: string;
  tag?: string;
  /** Open tickets due before the end of today (server clock) — same definition as the dashboard tile. */
  dueToday?: boolean;
}

export interface TicketStatistics {
  openCount: number;
  unassignedCount: number;
  dueTodayCount: number;
  breachedCount: number;
}

export interface TicketCustomerSummary {
  id: string;
  code: string;
  displayNameEn: string;
  displayNameAr: string;
  tier: string | null;
  primaryEmail: string | null;
  primaryPhone: string | null;
  isBlocked: boolean;
  openTicketCount: number;
}

export interface TicketTag {
  id: string;
  name: string;
  colorHex: string;
}

export interface TicketDetail {
  id: string;
  number: string;
  subject: string;
  description: string;
  language: string;
  customer: TicketCustomerSummary;
  categoryId: string;
  categoryNameEn: string;
  categoryNameAr: string;
  priorityId: string;
  priorityNameEn: string;
  priorityNameAr: string;
  priorityColorHex: string;
  statusId: string;
  statusNameEn: string;
  statusNameAr: string;
  statusColorHex: string;
  statusKind: TicketStatusKind;
  isTerminal: boolean;
  channel: ChannelKey;
  departmentId: string | null;
  departmentNameEn: string | null;
  departmentNameAr: string | null;
  assignedAgentId: string | null;
  assignedAgentNameEn: string | null;
  assignedAgentNameAr: string | null;
  firstResponseDueAt: string | null;
  resolutionDueAt: string | null;
  isFirstResponseBreached: boolean;
  isResolutionBreached: boolean;
  resolvedAt: string | null;
  resolutionNote: string | null;
  closedAt: string | null;
  reopenCount: number;
  customerReplyCount: number;
  escalationLevel: number;
  escalatedAt: string | null;
  mergedIntoTicketId: string | null;
  tags: TicketTag[];
  createdAt: string;
  modifiedAt: string | null;
  canUpdate: boolean;
  canReply: boolean;
  canAddInternalNote: boolean;
  canMerge: boolean;
  canAssign: boolean;
  canChangeStatus: boolean;
  canEscalate: boolean;
}

export interface CreateTicketRequest {
  customerId: string;
  subject: string;
  description: string;
  categoryId: string;
  priorityId?: string;
  departmentId?: string;
  channel: ChannelKey;
}

export interface UpdateTicketRequest {
  id: string;
  subject: string;
  description: string;
  categoryId: string;
  priorityId: string;
  tagIds: string[];
}

export interface ReplyToTicketRequest {
  bodyText: string;
  bodyHtml?: string;
}

export interface AddInternalNoteRequest {
  bodyText: string;
}

export interface MergeTicketsRequest {
  targetTicketId: string;
  reason?: string;
  allowCrossCustomer?: boolean;
}

/** One assignment candidate with the load and availability the picker shows inline. */
export interface AssignableAgent {
  id: string;
  nameEn: string;
  nameAr: string;
  availabilityStatus: string;
  openTickets: number;
  cap: number | null;
  isAvailable: boolean;
  warning: string | null;
}

export interface AssignTicketRequest {
  agentId?: string;
  teamId?: string;
  force?: boolean;
}

export interface BulkAssignRequest {
  ticketIds: string[];
  agentId?: string;
  teamId?: string;
  force?: boolean;
}

export interface BulkAssignItemResult {
  ticketId: string;
  number: string;
  succeeded: boolean;
  error: string | null;
}

export interface BulkAssignResult {
  succeeded: number;
  failed: number;
  results: BulkAssignItemResult[];
}

export interface ChangeTicketStatusRequest {
  statusId: string;
  /** Required only when the target status is Resolved-kind. */
  resolutionNote?: string;
}

export interface EscalateTicketRequest {
  reason: string;
}
