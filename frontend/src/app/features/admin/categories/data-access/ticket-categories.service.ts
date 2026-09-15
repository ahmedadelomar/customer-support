import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  CategoryDefaultOption,
  CreateTicketCategoryRequest,
  TicketCategoryAdmin,
  UpdateTicketCategoryRequest,
} from './interfaces/ticket-category.interface';

/** Data access for category-tree administration (Ticket Management / Categories and priorities). */
@Injectable({ providedIn: 'root' })
export class TicketCategoriesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/TicketCategories`;

  list(): Observable<TicketCategoryAdmin[]> {
    return this.#http.get<TicketCategoryAdmin[]>(this.#baseUrl);
  }

  create(request: CreateTicketCategoryRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateTicketCategoryRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  move(id: string, newParentId: string | null): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/move`, { newParentId });
  }

  deactivate(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/deactivate`, {});
  }

  activate(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/activate`, {});
  }

  /** Priorities and departments for the default pickers, read from the same lookups the ticket-creation form uses. */
  defaultOptions(): Observable<{ priorities: CategoryDefaultOption[]; departments: CategoryDefaultOption[] }> {
    return this.#http
      .get<{
        priorities: CategoryDefaultOption[];
        departments: CategoryDefaultOption[];
      }>(`${this.#config.apiUrl}/api/tickets/lookups`)
      .pipe(
        map((lookups) => ({
          priorities: lookups.priorities,
          departments: lookups.departments,
        })),
      );
  }
}
