import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, type Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  CreateQuickReplyRequest,
  PreviewQuickReplyRequest,
  QuickReply,
  QuickReplyQuery,
  QuickReplyRenderResult,
  SampleTicket,
  UpdateQuickReplyRequest,
} from './interfaces/quick-reply.interface';

/** Data access for quick replies (Agent Dashboard / Quick replies). */
@Injectable({ providedIn: 'root' })
export class QuickRepliesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  // `[Route("api/[controller]")]` derives this from the controller class name, so it is
  // PascalCase — matches the established convention (`/api/TicketPriorities`,
  // `/api/TicketCategories`, etc.), not the kebab-case the story's own contract table suggests.
  readonly #baseUrl = `${this.#config.apiUrl}/api/QuickReplies`;

  list(query: QuickReplyQuery = {}): Observable<QuickReply[]> {
    return this.#http.get<QuickReply[]>(this.#baseUrl, { params: this.#toParams(query) });
  }

  create(request: CreateQuickReplyRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateQuickReplyRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  render(id: string, ticketId: string): Observable<QuickReplyRenderResult> {
    return this.#http.post<QuickReplyRenderResult>(`${this.#baseUrl}/${id}/render`, { id, ticketId });
  }

  /** Live preview for the management form, before the reply is saved. */
  preview(request: PreviewQuickReplyRequest): Observable<QuickReplyRenderResult> {
    return this.#http.post<QuickReplyRenderResult>(`${this.#baseUrl}/preview`, request);
  }

  /** A small page of the caller's own tickets, for the preview form's sample-ticket picker — a
   *  mapped call rather than importing `TicketsService` from the sibling `agent/tickets` feature. */
  sampleTickets(): Observable<SampleTicket[]> {
    return this.#http
      .get<{ items: SampleTicket[] }>(`${this.#config.apiUrl}/api/tickets`, {
        params: new HttpParams().set('assignment', 'mine').set('pageSize', '10'),
      })
      .pipe(map((result) => result.items));
  }

  /** Category and channel options for the form, reusing the ticket lookups endpoint — same
   *  cross-feature-avoiding pattern as `sampleTickets()`. */
  lookups(): Observable<{
    categories: { id: string; nameEn: string; nameAr: string }[];
    channels: { id: string; nameEn: string; nameAr: string }[];
  }> {
    return this.#http.get<{
      categories: { id: string; nameEn: string; nameAr: string }[];
      channels: { id: string; nameEn: string; nameAr: string }[];
    }>(`${this.#config.apiUrl}/api/tickets/lookups`);
  }

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
