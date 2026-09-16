import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import type { AuditAction } from '../../../../core/models/enums';
import type { PagedResult } from '../../../../core/models/api.models';
import { AppConfigService } from '../../../../core/services/app-config.service';

export interface AuditLogListItem {
  id: string;
  occurredAt: string;
  userId: string | null;
  userName: string | null;
  action: AuditAction;
  entityType: string;
  entityId: string | null;
  ipAddress: string | null;
}

export interface FieldChange {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface AuditLogDetail extends AuditLogListItem {
  userAgent: string | null;
  correlationId: string | null;
  changes: FieldChange[];
}

export interface AuditQuery {
  from?: string;
  to?: string;
  userId?: string;
  entityType?: string;
  entityId?: string;
  action?: AuditAction;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Data access for the audit trail (Security & Administration / Audit logs). Read-only by design. */
@Injectable({ providedIn: 'root' })
export class AuditService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  // Kebab-case here, unlike the PascalCase controllers: AuditLogsController carries an explicit
  // [Route("api/audit-logs")] rather than deriving its route from the class name.
  readonly #baseUrl = `${this.#config.apiUrl}/api/audit-logs`;

  list(query: AuditQuery = {}): Observable<PagedResult<AuditLogListItem>> {
    return this.#http.get<PagedResult<AuditLogListItem>>(this.#baseUrl, { params: this.#toParams(query) });
  }

  getById(id: string): Observable<AuditLogDetail> {
    return this.#http.get<AuditLogDetail>(`${this.#baseUrl}/${id}`);
  }

  entityTypes(): Observable<string[]> {
    return this.#http.get<string[]>(`${this.#baseUrl}/entity-types`);
  }

  /** CSV of the current filter. Returned as a blob so the caller can trigger a download. */
  export(query: AuditQuery = {}): Observable<Blob> {
    return this.#http.get(`${this.#baseUrl}/export`, {
      params: this.#toParams(query),
      responseType: 'blob',
    });
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
