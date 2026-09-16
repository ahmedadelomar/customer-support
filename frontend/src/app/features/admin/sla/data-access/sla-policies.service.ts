import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { SlaPolicy, SlaPolicyPreviewResult, SlaPolicyRequest } from './interfaces/sla-policy.interface';

/** Data access for SLA policy administration (SLA and Automation / Response and resolution targets). */
@Injectable({ providedIn: 'root' })
export class SlaPoliciesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/SlaPolicies`;

  list(): Observable<SlaPolicy[]> {
    return this.#http.get<SlaPolicy[]>(this.#baseUrl);
  }

  create(request: SlaPolicyRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: SlaPolicyRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, { id, ...request });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }

  preview(id: string, priorityId: string, arrivalTime: string): Observable<SlaPolicyPreviewResult> {
    return this.#http.post<SlaPolicyPreviewResult>(`${this.#baseUrl}/${id}/preview`, { priorityId, arrivalTime });
  }
}
