import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { AutomationRunLog, AutomationRunLogFilter } from './interfaces/automation-run-log.interface';

/**
 * Cross-cutting automation data access shared by assignment (CS-502) and escalation (CS-503) rules:
 * the allow-listed condition field list and the decision log — "why did this go to Ahmed" and "why
 * did this escalate" are the same underlying log with a different filter.
 */
@Injectable({ providedIn: 'root' })
export class AutomationService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/Automation`;

  conditionFields(): Observable<string[]> {
    return this.#http.get<string[]>(`${this.#baseUrl}/condition-fields`);
  }

  log(filter: AutomationRunLogFilter = {}): Observable<AutomationRunLog[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.#http.get<AutomationRunLog[]>(`${this.#baseUrl}/log`, { params });
  }
}
