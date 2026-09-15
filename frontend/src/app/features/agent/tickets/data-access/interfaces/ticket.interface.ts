import type { ChannelKey, TicketStatusKind } from '../../../../../core/models/enums';
import type { PagedQuery } from '../../../../../core/models/api.models';
import type { CustomerContact } from '../../../customers/data-access/interfaces/contact.interface';

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

/** One of the customer's other open tickets, shown on the panel so duplicates are obvious. */
export interface CustomerPanelOtherTicket {
  id: string;
  number: string;
  subject: string;
  statusNameEn: string;
  statusNameAr: string;
  statusColorHex: string;
}

/** A pinned customer note, trimmed to what the panel shows. */
export interface CustomerPanelNote {
  id: string;
  body: string;
  authorNameEn: string | null;
  authorNameAr: string | null;
  createdAt: string;
}

/**
 * Everything the ticket screen's customer panel needs, arriving with the ticket in one response.
 * `canViewFull` is false when the caller lacks `customers.view` — every field below it is then just
 * a default, and the panel must show only the display name.
 */
export interface CustomerPanel {
  id: string;
  displayNameEn: string;
  displayNameAr: string;
  canViewFull: boolean;
  code: string;
  tier: string | null;
  preferredLanguage: string;
  preferredChannel: ChannelKey;
  isBlocked: boolean;
  blockedReason: string | null;
  satisfactionScore: number | null;
  lastInteractionAt: string | null;
  openTicketCount: number;
  contacts: CustomerContact[];
  otherOpenTickets: CustomerPanelOtherTicket[];
  otherOpenTicketCount: number;
  pinnedNotes: CustomerPanelNote[];
  canEdit: boolean;
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
  customer: CustomerPanel;
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
  /** Set when this reply was inserted from a quick reply, for attribution. */
  quickReplyId?: string;
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
  /** Optional context for the new assignee, recorded as an internal note. */
  handoverNote?: string;
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
  /** Closes anyway despite open linked tasks, leaving them open. */
  force?: boolean;
  /** Closes and completes every open linked task in the same action. */
  completeLinkedTasks?: boolean;
}

/** One line of the "you have open tasks" warning; matches `OpenTaskSummary` (backend). */
export interface OpenTaskSummary {
  id: string;
  title: string;
}

export interface EscalateTicketRequest {
  reason: string;
}
