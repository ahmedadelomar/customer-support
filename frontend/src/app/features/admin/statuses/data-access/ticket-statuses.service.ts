import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  CreateTicketStatusRequest,
  TicketStatusAdmin,
  UpdateTicketStatusRequest,
} from './interfaces/ticket-status.interface';

/** Data access for status-workflow administration (Ticket Management / Status workflow and escalation). */
@Injectable({ providedIn: 'root' })
export class TicketStatusesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/TicketStatuses`;

  list(): Observable<TicketStatusAdmin[]> {
    return this.#http.get<TicketStatusAdmin[]>(this.#baseUrl);
  }

  create(request: CreateTicketStatusRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateTicketStatusRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  reorder(orderedIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/reorder`, { orderedIds });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }
}
