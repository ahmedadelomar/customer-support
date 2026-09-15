import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { PagedResult } from '../../../../core/models/api.models';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { CreateNoteRequest, CustomerNote, UpdateNoteRequest } from '../customer.models';

/** Data access for one customer's notes. */
@Injectable({ providedIn: 'root' })
export class CustomerNotesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  #baseUrl(customerId: string): string {
    return `${this.#config.apiUrl}/api/customers/${customerId}/notes`;
  }

  list(customerId: string, page: number, pageSize: number): Observable<PagedResult<CustomerNote>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.#http.get<PagedResult<CustomerNote>>(this.#baseUrl(customerId), { params });
  }

  create(customerId: string, request: CreateNoteRequest): Observable<CustomerNote> {
    return this.#http.post<CustomerNote>(this.#baseUrl(customerId), request);
  }

  update(customerId: string, noteId: string, request: UpdateNoteRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl(customerId)}/${noteId}`, request);
  }

  delete(customerId: string, noteId: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl(customerId)}/${noteId}`);
  }

  togglePin(customerId: string, noteId: string): Observable<boolean> {
    return this.#http.post<boolean>(`${this.#baseUrl(customerId)}/${noteId}/pin`, {});
  }
}
