import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import type { PagedResult } from '../../../../core/models/api.models';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  CreateUserRequest,
  UpdateUserRequest,
  UserDetail,
  UserListItem,
  UserLookups,
  UserQuery,
} from './interfaces/user.interface';

/** Data access for user administration (Security & Administration / Users and roles). */
@Injectable({ providedIn: 'root' })
export class UsersService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  // PascalCase, like every other multi-word controller — `[Route("api/[controller]")]` derives it
  // from the class name.
  readonly #baseUrl = `${this.#config.apiUrl}/api/Users`;

  list(query: UserQuery = {}): Observable<PagedResult<UserListItem>> {
    return this.#http.get<PagedResult<UserListItem>>(this.#baseUrl, { params: this.#toParams(query) });
  }

  getById(id: string): Observable<UserDetail> {
    return this.#http.get<UserDetail>(`${this.#baseUrl}/${id}`);
  }

  lookups(): Observable<UserLookups> {
    return this.#http.get<UserLookups>(`${this.#baseUrl}/lookups`);
  }

  create(request: CreateUserRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateUserRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  activate(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/activate`, {});
  }

  deactivate(id: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/deactivate`, {});
  }

  updateRoles(id: string, roleIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}/roles`, { roleIds });
  }

  /** Sets the signed-in user's own availability. */
  updateMyAvailability(availabilityStatus: string): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/me/availability`, { availabilityStatus });
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
