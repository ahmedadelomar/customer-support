import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';

export interface Branch {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  timeZoneId: string | null;
  address: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  userCount: number;
  openTicketCount: number;
}

/** A curated list rather than the full IANA database, which is far too long for a picker. */
export const TIME_ZONES = [
  'Asia/Riyadh',
  'Asia/Dubai',
  'Asia/Kuwait',
  'Asia/Qatar',
  'Asia/Bahrain',
  'Africa/Cairo',
  'Europe/London',
  'UTC',
] as const;

/** Data access for branches (Platform / Branch scoping and the branch switcher). */
@Injectable({ providedIn: 'root' })
export class BranchesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/Branches`;

  list(includeInactive = false): Observable<Branch[]> {
    return this.#http.get<Branch[]>(this.#baseUrl, {
      params: new HttpParams().set('includeInactive', includeInactive),
    });
  }

  create(request: {
    code: string;
    nameEn: string;
    nameAr: string;
    timeZoneId?: string;
    address?: string;
    phoneNumber?: string;
  }): Observable<string> {
    return this.#http.post<string>(this.#baseUrl, request);
  }

  update(
    id: string,
    request: { nameEn: string; nameAr: string; timeZoneId?: string; address?: string; phoneNumber?: string },
  ): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${id}`, request);
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    return this.#http.post<void>(`${this.#baseUrl}/${id}/${isActive ? 'activate' : 'deactivate'}`, {});
  }
}
