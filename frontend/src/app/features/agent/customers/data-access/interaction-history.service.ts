import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { InteractionPage, InteractionQuery } from './interfaces/interaction.interface';

/** Data access for one customer's interaction timeline. Read-only — see `CustomerInteractionsController`. */
@Injectable({ providedIn: 'root' })
export class InteractionHistoryService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  list(customerId: string, query: InteractionQuery): Observable<InteractionPage> {
    return this.#http.get<InteractionPage>(`${this.#config.apiUrl}/api/customers/${customerId}/interactions`, {
      params: this.#toParams(query),
    });
  }

  /** Skips empty values so an unset filter never reaches the server as an ambiguous blank one. */
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
