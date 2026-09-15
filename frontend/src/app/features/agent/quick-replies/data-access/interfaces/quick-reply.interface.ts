/** "Personal" | "Team" | "Global". */
export type QuickReplyScope = 'Personal' | 'Team' | 'Global';

export interface QuickReply {
  id: string;
  scope: QuickReplyScope;
  ownerId: string | null;
  teamId: string | null;
  teamNameEn: string | null;
  teamNameAr: string | null;
  shortcut: string | null;
  titleEn: string;
  titleAr: string;
  bodyEn: string;
  bodyAr: string;
  categoryId: string | null;
  categoryNameEn: string | null;
  categoryNameAr: string | null;
  channelId: string | null;
  channelNameEn: string | null;
  channelNameAr: string | null;
  usageCount: number;
  isActive: boolean;
  /** Resolved server-side: manage permission, plus the global permission for Global scope. */
  canEdit: boolean;
}

export interface QuickReplyQuery {
  categoryId?: string;
  channelId?: string;
  scope?: QuickReplyScope;
  includeInactive?: boolean;
}

export interface CreateQuickReplyRequest {
  scope: QuickReplyScope;
  teamId?: string;
  shortcut?: string;
  titleEn: string;
  titleAr: string;
  bodyEn: string;
  bodyAr: string;
  categoryId?: string;
  channelId?: string;
}

export interface UpdateQuickReplyRequest extends CreateQuickReplyRequest {
  id: string;
  isActive: boolean;
}

/** Mirrors `RenderResult`. */
export interface QuickReplyRenderResult {
  body: string;
  unresolvedTokens: string[];
}

export interface PreviewQuickReplyRequest {
  ticketId: string;
  bodyEn: string;
  bodyAr: string;
}

/** A minimal ticket option for the management screen's sample-ticket preview picker. */
export interface SampleTicket {
  id: string;
  number: string;
  subject: string;
}

/** Every placeholder token the resolver supports — for the management screen's insert-at-cursor palette. */
export const QUICK_REPLY_TOKENS: string[] = [
  'customer.displayName',
  'customer.firstName',
  'customer.code',
  'customer.tier',
  'ticket.number',
  'ticket.subject',
  'ticket.status',
  'ticket.priority',
  'ticket.category',
  'agent.displayName',
  'agent.jobTitle',
  'org.supportEmail',
  'org.supportPhone',
];
