import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../core/services/app-config.service';
import type {
  AppNotification,
  NotificationPreference,
  NotificationPreferenceInput,
} from './interfaces/notification.interface';

/** Data access for the signed-in user's own notifications and channel preferences (SLA and Automation / Alerts and notifications). */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/Notifications`;
  readonly #preferencesUrl = `${this.#config.apiUrl}/api/notification-preferences`;

  list(skip = 0, take = 30): Observable<AppNotification[]> {
    const params = new HttpParams().set('skip', skip).set('take', take);
    return this.#http.get<AppNotification[]>(this.#baseUrl, { params });
  }

  unreadCount(): Observable<number> {
    return this.#http.get<number>(`${this.#baseUrl}/unread-count`);
  }

  markRead(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/read-all`, {});
  }

  preferences(): Observable<NotificationPreference[]> {
    return this.#http.get<NotificationPreference[]>(this.#preferencesUrl);
  }

  updatePreferences(preferences: NotificationPreferenceInput[]): Observable<void> {
    return this.#http.put<void>(this.#preferencesUrl, { preferences });
  }
}
