import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  ChannelAccount,
  ChannelAccountRequest,
  ChannelLookup,
  TestChannelAccountConnectionResult,
} from './interfaces/channel-account.interface';

/** Data access for channel account administration (Communication Channels / Email channel and beyond). */
@Injectable({ providedIn: 'root' })
export class ChannelAccountsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/ChannelAccounts`;

  list(): Observable<ChannelAccount[]> {
    return this.#http.get<ChannelAccount[]>(this.#baseUrl);
  }

  channels(): Observable<ChannelLookup[]> {
    return this.#http.get<ChannelLookup[]>(`${this.#baseUrl}/channels`);
  }

  create(request: ChannelAccountRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: ChannelAccountRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  test(id: string): Observable<TestChannelAccountConnectionResult> {
    return this.#http.post<TestChannelAccountConnectionResult>(`${this.#baseUrl}/${id}/test`, {});
  }
}
