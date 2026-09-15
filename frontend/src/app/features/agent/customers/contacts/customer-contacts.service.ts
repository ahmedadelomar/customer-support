import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type {
  AddContactRequest,
  ConfirmVerificationResult,
  CustomerContact,
  SendVerificationResult,
  UpdateContactRequest,
} from '../customer.models';

/** Data access for one customer's contacts. Nested under the customer id, matching the API routes. */
@Injectable({ providedIn: 'root' })
export class CustomerContactsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  #baseUrl(customerId: string): string {
    return `${this.#config.apiUrl}/api/customers/${customerId}/contacts`;
  }

  list(customerId: string): Observable<CustomerContact[]> {
    return this.#http.get<CustomerContact[]>(this.#baseUrl(customerId));
  }

  add(customerId: string, request: AddContactRequest): Observable<CustomerContact> {
    return this.#http.post<CustomerContact>(this.#baseUrl(customerId), request);
  }

  update(customerId: string, contactId: string, request: UpdateContactRequest): Observable<CustomerContact> {
    return this.#http.put<CustomerContact>(`${this.#baseUrl(customerId)}/${contactId}`, request);
  }

  delete(customerId: string, contactId: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl(customerId)}/${contactId}`);
  }

  setPrimary(customerId: string, contactId: string): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl(customerId)}/${contactId}/primary`, {});
  }

  sendVerification(customerId: string, contactId: string): Observable<SendVerificationResult> {
    return this.#http.post<SendVerificationResult>(`${this.#baseUrl(customerId)}/${contactId}/verify/send`, {});
  }

  confirmVerification(
    customerId: string,
    contactId: string,
    code: string,
  ): Observable<ConfirmVerificationResult> {
    return this.#http.post<ConfirmVerificationResult>(
      `${this.#baseUrl(customerId)}/${contactId}/verify/confirm`,
      { code },
    );
  }
}
