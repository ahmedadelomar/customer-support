import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';

/** Mirrors the server's `SettingSource`: where the resolved value actually came from. */
export enum SettingSource {
  Default = 0,
  Global = 1,
  Branch = 2,
}

export interface Setting {
  key: string;
  category: string;
  dataType: string;
  nameEn: string;
  nameAr: string;
  descriptionEn: string | null;
  descriptionAr: string | null;
  value: string | null;
  source: SettingSource;
  isSecret: boolean;
  isSystem: boolean;
}

export interface SettingCategory {
  category: string;
  settings: Setting[];
}

/** The mask the server returns for secrets. Resubmitting it leaves the stored value alone. */
export const SECRET_MASK = '********';

/** Data access for runtime configuration (Security & Administration / System configuration). */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/Settings`;

  list(branchId?: string): Observable<SettingCategory[]> {
    let params = new HttpParams();
    if (branchId) params = params.set('branchId', branchId);

    return this.#http.get<SettingCategory[]>(this.#baseUrl, { params });
  }

  update(key: string, value: string | null, branchId?: string): Observable<void> {
    return this.#http.put<void>(`${this.#baseUrl}/${key}`, { value, branchId: branchId ?? null });
  }

  removeOverride(key: string, branchId: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${key}/override`, {
      params: new HttpParams().set('branchId', branchId),
    });
  }
}
