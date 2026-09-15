import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, type Observable } from 'rxjs';
import type { PagedResult } from '../../../../core/models/api.models';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { AgentTask, CreateTaskRequest, TaskQuery, UpdateTaskRequest } from './interfaces/task.interface';

/** The handful of priority fields the compact task composer needs. */
export interface TaskPriorityOption {
  id: string;
  nameEn: string;
  nameAr: string;
  colorHex: string;
}

/** Data access for agent tasks (Agent Dashboard / Tasks and reminders). */
@Injectable({ providedIn: 'root' })
export class TasksService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/tasks`;

  list(query: TaskQuery): Observable<PagedResult<AgentTask>> {
    return this.#http.get<PagedResult<AgentTask>>(this.#baseUrl, { params: this.#toParams(query) });
  }

  ticketTasks(ticketId: string): Observable<AgentTask[]> {
    return this.#http.get<AgentTask[]>(`${this.#config.apiUrl}/api/tickets/${ticketId}/tasks`);
  }

  create(request: CreateTaskRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateTaskRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  complete(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/complete`, {});
  }

  /**
   * Priorities reused from the ticket lookups endpoint (tasks reuse `TicketPriority` so tasks and
   * tickets sort consistently) — a small mapped call rather than importing `TicketsService` from the
   * sibling `agent/tickets` feature, following `ticket-categories.service.ts`'s precedent.
   */
  priorityOptions(): Observable<TaskPriorityOption[]> {
    return this.#http
      .get<{ priorities: { id: string; nameEn: string; nameAr: string; colorHex: string }[] }>(
        `${this.#config.apiUrl}/api/tickets/lookups`,
      )
      .pipe(map((lookups) => lookups.priorities));
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
