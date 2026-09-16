import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { StartChatResult, WidgetChatMessage, WidgetChatSession } from './interfaces/widget-chat.interface';

export interface StartChatRequest {
  channelAccountId: string;
  visitorKey: string | null;
  visitorName: string | null;
  pageUrl: string | null;
  language: string;
  initialMessage: string | null;
}

/**
 * REST data access for the widget side of live chat. Unauthenticated by the app's normal sense —
 * every call after start carries the visitor's own scoped token by hand, since `authInterceptor`
 * only attaches a token from a real agent login (`AuthService.accessToken`, which a visitor never has).
 */
@Injectable({ providedIn: 'root' })
export class WidgetChatService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/chat`;

  start(request: StartChatRequest): Observable<StartChatResult> {
    return this.#http.post<StartChatResult>(`${this.#baseUrl}/sessions`, request);
  }

  messages(sessionId: string, token: string): Observable<WidgetChatMessage[]> {
    return this.#http.get<WidgetChatMessage[]>(`${this.#baseUrl}/sessions/${sessionId}/messages`, this.#auth(token));
  }

  identify(sessionId: string, token: string, name: string | null, email: string | null): Observable<WidgetChatSession> {
    return this.#http.post<WidgetChatSession>(
      `${this.#baseUrl}/sessions/${sessionId}/identify`, { name, email, phone: null }, this.#auth(token),
    );
  }

  end(sessionId: string, token: string): Observable<WidgetChatSession> {
    return this.#http.post<WidgetChatSession>(`${this.#baseUrl}/sessions/${sessionId}/end`, {}, this.#auth(token));
  }

  rate(sessionId: string, token: string, rating: number): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/sessions/${sessionId}/rate`, { rating }, this.#auth(token));
  }

  #auth(token: string) {
    return { headers: new HttpHeaders({ Authorization: `Bearer ${token}` }) };
  }
}
