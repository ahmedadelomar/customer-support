export const ChatAuthorType = { Customer: 0, Agent: 1, System: 2, Bot: 3 } as const;

export interface WidgetChatMessage {
  id: string;
  chatSessionId: string;
  authorType: number;
  authorId: string | null;
  authorDisplayName: string | null;
  body: string;
  isSystemMessage: boolean;
  sentAt: string;
  readAt: string | null;
}

export interface WidgetChatSession {
  id: string;
  visitorKey: string;
  visitorName: string | null;
  visitorEmail: string | null;
  customerId: string | null;
  ticketId: string | null;
  assignedAgentId: string | null;
  assignedAgentName: string | null;
  status: string;
  startedAt: string;
  endedAt: string | null;
}

export interface StartChatResult {
  sessionId: string;
  visitorKey: string;
  token: string;
  session: WidgetChatSession;
}

/** What the widget keeps in `localStorage` to resume a session after a page reload. */
export interface StoredChatSession {
  channelAccountId: string;
  sessionId: string;
  visitorKey: string;
  token: string;
}
