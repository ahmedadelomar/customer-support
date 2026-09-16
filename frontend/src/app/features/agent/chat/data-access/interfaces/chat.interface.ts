/** Mirrors the backend's <c>MessageAuthorType</c> enum. */
export const ChatAuthorType = { Customer: 0, Agent: 1, System: 2, Bot: 3 } as const;

export interface ChatMessage {
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

export interface ChatSession {
  id: string;
  visitorKey: string;
  visitorName: string | null;
  visitorEmail: string | null;
  customerId: string | null;
  ticketId: string | null;
  assignedAgentId: string | null;
  assignedAgentName: string | null;
  queuedForTeamId: string | null;
  status: string;
  language: string;
  pageUrl: string | null;
  startedAt: string;
  firstAgentReplyAt: string | null;
  endedAt: string | null;
  messageCount: number;
  unreadCount: number;
  rating: number | null;
}

export interface ChatQueue {
  waiting: ChatSession[];
  active: ChatSession[];
}

export interface PromoteChatRequest {
  categoryId: string;
  priorityId?: string | null;
  departmentId?: string | null;
}
