import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  EscalationRule,
  EscalationRuleRequest,
  EscalationTestMatch,
} from './interfaces/escalation-rule.interface';

/** Data access for escalation rule administration (SLA and Automation / Escalation rules). */
@Injectable({ providedIn: 'root' })
export class EscalationRulesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/EscalationRules`;

  list(): Observable<EscalationRule[]> {
    return this.#http.get<EscalationRule[]>(this.#baseUrl);
  }

  create(request: EscalationRuleRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: EscalationRuleRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, { id, ...request });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  reorder(orderedIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/reorder`, { orderedIds });
  }

  /** Dry-run: the currently open tickets this rule would fire on, with why. Fires nothing. */
  test(id: string): Observable<EscalationTestMatch[]> {
    return this.#http.post<EscalationTestMatch[]>(`${this.#baseUrl}/${id}/test`, {});
  }
}
