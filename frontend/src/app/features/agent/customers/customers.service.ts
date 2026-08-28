import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { PagedResult } from '../../../core/models/api.models';
import { AppConfigService } from '../../../core/services/app-config.service';
import type {
  CreateCustomerRequest,
  CustomerDetail,
  CustomerListItem,
  CustomerQuery,
  UpdateCustomerRequest,
} from './customer.models';

/**
 * Data access for customer profiles. This is the reference shape for every feature service:
 * one injectable, plain `HttpClient`, request objects in and typed responses out. State lives
 * in the components as signals, not here.
 */
@Injectable({ providedIn: 'root' })
export class CustomersService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/customers`;

  list(query: CustomerQuery): Observable<PagedResult<CustomerListItem>> {
    return this.#http.get<PagedResult<CustomerListItem>>(this.#baseUrl, {
      params: this.#toParams(query),
    });
  }

  getById(id: string): Observable<CustomerDetail> {
    return this.#http.get<CustomerDetail>(`${this.#baseUrl}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(request: UpdateCustomerRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${request.id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  /**
   * Builds the query string, skipping empty values so the URL stays clean and the server
   * receives no ambiguous blank filters.
   */
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
