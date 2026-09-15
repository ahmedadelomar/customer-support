import type { ChannelKey, MessageDirection } from '../../../../../core/models/enums';

/** Mirrors `InteractionDto`. */
export interface Interaction {
  id: string;
  ticketId?: string;
  ticketNumber?: string;
  channel: ChannelKey;
  direction: MessageDirection;
  subject?: string;
  preview?: string;
  agentId?: string;
  agentNameEn?: string;
  agentNameAr?: string;
  occurredAt: string;
}

/** Mirrors `InteractionPageDto`. Not `PagedResult<T>` — this is keyset-paged, with no total count. */
export interface InteractionPage {
  items: Interaction[];
  hasMore: boolean;
}

/** Query parameters accepted by `GET /api/customers/{id}/interactions`. */
export interface InteractionQuery {
  before?: string;
  beforeId?: string;
  channel?: ChannelKey;
  direction?: MessageDirection;
  from?: string;
  to?: string;
  pageSize?: number;
}
