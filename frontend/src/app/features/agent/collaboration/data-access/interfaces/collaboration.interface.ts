export interface Mention {
  id: string;
  ticketId: string;
  ticketNumber: string;
  ticketSubject: string;
  mentionedById: string;
  mentionedByNameEn: string;
  mentionedByNameAr: string;
  excerptEn: string;
  mentionedAt: string;
  readAt: string | null;
}

export interface MentionableUser {
  id: string;
  nameEn: string;
  nameAr: string;
  /** False when this colleague would not currently see the ticket — warn, never exclude. */
  canView: boolean;
}

export interface Watcher {
  userId: string;
  nameEn: string;
  nameAr: string;
  addedByAutomation: boolean;
  addedAt: string;
  isCurrentUser: boolean;
}

/** One viewer's live presence on a ticket, broadcast by the collaboration hub. */
export interface PresenceEntry {
  userId: string;
  displayName: string;
  isComposing: boolean;
}
