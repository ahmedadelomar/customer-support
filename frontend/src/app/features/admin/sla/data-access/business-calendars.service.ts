import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { BusinessCalendar, BusinessCalendarRequest } from './interfaces/business-calendar.interface';

/** Data access for business calendar administration (SLA and Automation / Response and resolution targets). */
@Injectable({ providedIn: 'root' })
export class BusinessCalendarsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #baseUrl = `${this.#config.apiUrl}/api/SlaCalendars`;

  list(): Observable<BusinessCalendar[]> {
    return this.#http.get<BusinessCalendar[]>(this.#baseUrl);
  }

  create(request: BusinessCalendarRequest): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(id: string, request: BusinessCalendarRequest): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, { id, ...request });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${id}`);
  }
}
