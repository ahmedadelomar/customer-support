import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { Mention, MentionableUser, Watcher } from './interfaces/collaboration.interface';

/**
 * Data access for team collaboration (Agent Dashboard / Team collaboration): the mentions inbox plus
 * ticket-scoped watchers and the mention picker's candidate list. Calls `/api/Tickets/...` directly
 * rather than importing `TicketsService` — the same cross-feature-avoiding pattern `QuickRepliesService`
 * already uses for its own ticket lookups.
 */
@Injectable({ providedIn: 'root' })
export class CollaborationService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  mentions(): Observable<Mention[]> {
    return this.#http.get<Mention[]>(`${this.#config.apiUrl}/api/Mentions`);
  }

  markMentionRead(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#config.apiUrl}/api/Mentions/${id}/read`, {});
  }

  watch(ticketId: string): Observable<void> {
    return this.#http.post<void>(`${this.#config.apiUrl}/api/Tickets/${ticketId}/watch`, {});
  }

  unwatch(ticketId: string): Observable<void> {
    return this.#http.post<void>(`${this.#config.apiUrl}/api/Tickets/${ticketId}/unwatch`, {});
  }

  watchers(ticketId: string): Observable<Watcher[]> {
    return this.#http.get<Watcher[]>(`${this.#config.apiUrl}/api/Tickets/${ticketId}/watchers`);
  }

  mentionable(ticketId: string, search?: string): Observable<MentionableUser[]> {
    let params = new HttpParams();
    if (search) params = params.set('search', search);

    return this.#http.get<MentionableUser[]>(
      `${this.#config.apiUrl}/api/Tickets/${ticketId}/mentionable`,
      { params },
    );
  }
}
