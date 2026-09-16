export interface AppNotification {
  id: string;
  eventType: string;
  titleEn: string;
  titleAr: string;
  bodyEn: string;
  bodyAr: string;
  link: string | null;
  severity: 'Info' | 'Warning' | 'Critical';
  createdAt: string;
  readAt: string | null;
}

/** Pushed over `/hubs/notifications` the instant a notification row commits. Same shape as `AppNotification` minus `readAt` — it is always unread on arrival. */
export type RealtimeNotification = Omit<AppNotification, 'readAt'>;

/** Mirrors `NotificationArea`: Tickets = 0, Sla = 1, Tasks = 2, Collaboration = 3. */
export type NotificationArea = 0 | 1 | 2 | 3;

export interface NotificationPreference {
  eventType: string;
  area: NotificationArea;
  viaInApp: boolean;
  viaEmail: boolean;
  viaSms: boolean;
  viaPush: boolean;
  /** True when the caller has no saved row — shown as "default" until they change it. */
  isUsingDefault: boolean;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
}

export interface NotificationPreferenceInput {
  eventType: string;
  viaInApp: boolean;
  viaEmail: boolean;
  viaSms: boolean;
  viaPush: boolean;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
}
