import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { CreateSavedTicketViewRequest, SavedTicketView } from './interfaces/saved-ticket-view.interface';

/** Data access for a user's saved ticket-list filter sets. */
@Injectable({ providedIn: 'root' })
export class SavedTicketViewsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/tickets/views`;

  list(): Observable<SavedTicketView[]> {
    return this.#http.get<SavedTicketView[]>(this.#baseUrl);
  }

  create(request: CreateSavedTicketViewRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }
}
