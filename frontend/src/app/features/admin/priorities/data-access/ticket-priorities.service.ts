import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  CreateTicketPriorityRequest,
  TicketPriorityAdmin,
  UpdateTicketPriorityRequest,
} from './interfaces/ticket-priority.interface';

/** Data access for priority-scale administration (Ticket Management / Categories and priorities). */
@Injectable({ providedIn: 'root' })
export class TicketPrioritiesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/TicketPriorities`;

  list(): Observable<TicketPriorityAdmin[]> {
    return this.#http.get<TicketPriorityAdmin[]>(this.#baseUrl);
  }

  create(request: CreateTicketPriorityRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateTicketPriorityRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  reorder(orderedIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/reorder`, { orderedIds });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }
}
