import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { PublicWebFormSchema, SubmitWebFormResult } from './interfaces/web-form.interface';

export interface SubmitWebFormRequest {
  values: Record<string, string>;
  captchaToken: string | null;
}

/** Anonymous data access for the public side of web forms (Communication Channels / Web forms, CS-305). */
@Injectable({ providedIn: 'root' })
export class PublicWebFormsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/public/forms`;

  getSchema(key: string): Observable<PublicWebFormSchema> {
    return this.#http.get<PublicWebFormSchema>(`${this.#baseUrl}/${key}`);
  }

  submit(key: string, request: SubmitWebFormRequest): Observable<SubmitWebFormResult> {
    return this.#http.post<SubmitWebFormResult>(`${this.#baseUrl}/${key}/submit`, request);
  }
}
