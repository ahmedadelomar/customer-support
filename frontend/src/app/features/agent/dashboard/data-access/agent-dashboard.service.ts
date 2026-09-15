import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { AgentDashboard, AgentQueueItem } from './interfaces/agent-dashboard.interface';

/** Data access for the agent dashboard (Agent Dashboard / Assigned tickets). */
@Injectable({ providedIn: 'root' })
export class AgentDashboardService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/dashboard`;

  get(): Observable<AgentDashboard> {
    return this.#http.get<AgentDashboard>(`${this.#baseUrl}/agent`);
  }

  /** Just the next-up queue — cheaper than `get()` for the auto-refresh poll. */
  queue(): Observable<AgentQueueItem[]> {
    return this.#http.get<AgentQueueItem[]>(`${this.#baseUrl}/agent/queue`);
  }
}
