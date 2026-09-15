import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { PagedResult } from '../../../../core/models/api.models';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { TicketLookups } from './interfaces/ticket-lookups.interface';
import type { TicketMessage } from './interfaces/ticket-message.interface';
import type {
  AddInternalNoteRequest,
  CreateTicketRequest,
  MergeTicketsRequest,
  ReplyToTicketRequest,
  TicketDetail,
  TicketListItem,
  TicketQuery,
  TicketStatistics,
  UpdateTicketRequest,
} from './interfaces/ticket.interface';

/** Data access for tickets (Ticket Management / Create and track tickets). Follows `CustomersService`. */
@Injectable({ providedIn: 'root' })
export class TicketsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/tickets`;

  list(query: TicketQuery): Observable<PagedResult<TicketListItem>> {
    return this.#http.get<PagedResult<TicketListItem>>(this.#baseUrl, { params: this.#toParams(query) });
  }

  statistics(query: TicketQuery): Observable<TicketStatistics> {
    return this.#http.get<TicketStatistics>(`${this.#baseUrl}/statistics`, { params: this.#toParams(query) });
  }

  lookups(): Observable<TicketLookups> {
    return this.#http.get<TicketLookups>(`${this.#baseUrl}/lookups`);
  }

  getById(id: string): Observable<TicketDetail> {
    return this.#http.get<TicketDetail>(`${this.#baseUrl}/${id}`);
  }

  messages(id: string, page: number, pageSize: number): Observable<PagedResult<TicketMessage>> {
    return this.#http.get<PagedResult<TicketMessage>>(`${this.#baseUrl}/${id}/messages`, {
      params: this.#toParams({ page, pageSize }),
    });
  }

  create(request: CreateTicketRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateTicketRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  reply(id: string, request: ReplyToTicketRequest): Observable<string> {
    return this.#http.post<string>(`${this.#baseUrl}/${id}/reply`, request);
  }

  addInternalNote(id: string, request: AddInternalNoteRequest): Observable<string> {
    return this.#http.post<string>(`${this.#baseUrl}/${id}/note`, request);
  }

  merge(id: string, request: MergeTicketsRequest): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/merge`, request);
  }

  /** Builds the query string, skipping empty values so the URL stays clean. */
  #toParams(query: object): HttpParams {
    let params = new HttpParams();

    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') {
        continue;
      }
      params = params.set(key, String(value));
    }

    return params;
  }
}
