import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  AssignmentPreview,
  AssignmentRule,
  AssignmentRuleRequest,
} from './interfaces/assignment-rule.interface';

/** Data access for assignment rule administration (SLA and Automation / Automatic assignment). */
@Injectable({ providedIn: 'root' })
export class AssignmentRulesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/AssignmentRules`;

  list(): Observable<AssignmentRule[]> {
    return this.#http.get<AssignmentRule[]>(this.#baseUrl);
  }

  create(request: AssignmentRuleRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: AssignmentRuleRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, { id, ...request });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  reorder(orderedIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/reorder`, { orderedIds });
  }

  /** Runs the rule against a real ticket and reports the outcome, writing nothing. */
  test(id: string, ticketId: string): Observable<AssignmentPreview> {
    return this.#http.post<AssignmentPreview>(`${this.#baseUrl}/${id}/test`, { ticketId });
  }
}
