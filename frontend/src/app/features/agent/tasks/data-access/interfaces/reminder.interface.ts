/** One fired-but-unacknowledged reminder — the persistent toast queue. */
export interface PendingReminder {
  id: string;
  message: string;
  sentAt: string;
  ticketId: string | null;
  ticketNumber: string | null;
}

export interface SnoozeReminderRequest {
  id: string;
  minutes: number;
}
