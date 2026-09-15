import type { AgentTaskStatus, NotificationChannel } from '../../../../../core/models/enums';

export interface TaskReminder {
  id: string;
  message: string;
  remindAt: string;
  channels: NotificationChannel[];
  isSent: boolean;
  sentAt: string | null;
  isDismissed: boolean;
}

export interface AgentTask {
  id: string;
  ticketId: string | null;
  ticketNumber: string | null;
  customerId: string | null;
  customerDisplayNameEn: string | null;
  customerDisplayNameAr: string | null;
  assignedToId: string;
  assignedToNameEn: string | null;
  assignedToNameAr: string | null;
  title: string;
  description: string | null;
  status: AgentTaskStatus;
  priorityId: string | null;
  priorityNameEn: string | null;
  priorityNameAr: string | null;
  priorityColorHex: string | null;
  dueAt: string | null;
  isOverdue: boolean;
  completedAt: string | null;
  reminders: TaskReminder[];
  canComplete: boolean;
  canEdit: boolean;
}

export interface TaskQuery {
  assignedToId?: string;
  status?: AgentTaskStatus;
  dueFrom?: string;
  dueTo?: string;
  page?: number;
  pageSize?: number;
}

/** A naive local date-time (no offset) plus the channels to notify on — matches `CreateTaskReminderInput`. */
export interface CreateTaskReminderRequest {
  remindAtLocal: string;
  channels: NotificationChannel[];
}

export interface CreateTaskRequest {
  ticketId?: string;
  customerId?: string;
  assignedToId: string;
  title: string;
  description?: string;
  priorityId?: string;
  dueAt?: string;
  reminder?: CreateTaskReminderRequest;
}

export interface UpdateTaskRequest {
  id: string;
  assignedToId: string;
  title: string;
  description?: string;
  priorityId?: string;
  dueAt?: string;
}
