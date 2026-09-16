import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { ChatMessage, ChatQueue, ChatSession, PromoteChatRequest } from './interfaces/chat.interface';

/** REST data access for the agent side of live chat (Communication Channels / Live chat, CS-303). */
@Injectable({ providedIn: 'root' })
export class ChatService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/chat`;

  queue(): Observable<ChatQueue> {
    return this.#http.get<ChatQueue>(`${this.#baseUrl}/queue`);
  }

  messages(sessionId: string): Observable<ChatMessage[]> {
    return this.#http.get<ChatMessage[]>(`${this.#baseUrl}/sessions/${sessionId}/messages`);
  }

  accept(sessionId: string): Observable<ChatSession> {
    return this.#http.post<ChatSession>(`${this.#baseUrl}/sessions/${sessionId}/accept`, {});
  }

  end(sessionId: string): Observable<ChatSession> {
    return this.#http.post<ChatSession>(`${this.#baseUrl}/sessions/${sessionId}/end`, {});
  }

  promote(sessionId: string, request: PromoteChatRequest): Observable<string> {
    return this.#http.post<string>(`${this.#baseUrl}/sessions/${sessionId}/promote`, request);
  }
}
