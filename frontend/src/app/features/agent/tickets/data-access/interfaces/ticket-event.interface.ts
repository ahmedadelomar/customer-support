import type { ChannelKey, MessageAuthorType, MessageDirection, TicketEventType } from '../../../../../core/models/enums';

/** One row of a ticket's event timeline (Ticket Management / Ticket history). */
export interface TicketEvent {
  id: string;
  ticketId: string;
  eventType: TicketEventType;
  actorId: string | null;
  actorDisplayName: string | null;
  isSystemGenerated: boolean;
  field: string | null;
  oldValue: string | null;
  newValue: string | null;
  oldDisplayValue: string | null;
  newDisplayValue: string | null;
  metadataJson: string | null;
  triggeredByRule: string | null;
  occurredAt: string;
}

export interface TicketHistoryPage {
  items: TicketEvent[];
  hasMore: boolean;
}

/** Keyset cursor plus filters for `GET /api/tickets/{id}/history`. */
export interface TicketHistoryQuery {
  before?: string;
  beforeId?: string;
  eventTypes?: TicketEventType[];
  includeSystem?: boolean;
  pageSize?: number;
}

/** One row of the merged event+message timeline; `kind` selects which optional fields apply. */
export interface TicketTimelineEntry {
  kind: 'event' | 'message';
  id: string;
  occurredAt: string;

  eventType: TicketEventType | null;
  actorId: string | null;
  actorDisplayName: string | null;
  isSystemGenerated: boolean | null;
  oldDisplayValue: string | null;
  newDisplayValue: string | null;
  metadataJson: string | null;
  triggeredByRule: string | null;

  channel: ChannelKey | null;
  direction: MessageDirection | null;
  authorType: MessageAuthorType | null;
  subject: string | null;
  bodyText: string | null;
  isInternalNote: boolean | null;
}

export interface TicketTimelinePage {
  items: TicketTimelineEntry[];
  hasMore: boolean;
}
