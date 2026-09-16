import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { WebFormDefinition, WebFormDefinitionRequest, WebFormSubmission } from './interfaces/web-form-admin.interface';

/** Data access for web form administration (Communication Channels / Web forms, CS-305). */
@Injectable({ providedIn: 'root' })
export class WebFormsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/web-forms`;

  list(): Observable<WebFormDefinition[]> {
    return this.#http.get<WebFormDefinition[]>(this.#baseUrl);
  }

  create(request: WebFormDefinitionRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: WebFormDefinitionRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, request);
  }

  submissions(formId: string): Observable<WebFormSubmission[]> {
    return this.#http.get<WebFormSubmission[]>(`${this.#baseUrl}/${formId}/submissions`);
  }

  retry(submissionId: string): Observable<string | null> {
    return this.#http.post<string | null>(`${this.#baseUrl}/submissions/${submissionId}/retry`, {});
  }
}
