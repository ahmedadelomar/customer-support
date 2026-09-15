import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { PendingReminder } from './interfaces/reminder.interface';

/** Data access for reminders — the persistent toast queue, snooze and dismiss. */
@Injectable({ providedIn: 'root' })
export class RemindersService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/reminders`;

  pending(): Observable<PendingReminder[]> {
    return this.#http.get<PendingReminder[]>(`${this.#baseUrl}/pending`);
  }

  snooze(id: string, minutes: number): Observable<string> {
    return this.#http.post<string>(`${this.#baseUrl}/${id}/snooze`, { id, minutes });
  }

  dismiss(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/dismiss`, {});
  }
}
