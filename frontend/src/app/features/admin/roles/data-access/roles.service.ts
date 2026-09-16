import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';

export interface Role {
  id: string;
  name: string;
  displayNameEn: string;
  displayNameAr: string;
  description: string | null;
  isSystem: boolean;
  userCount: number;
  permissionIds: string[];
  permissionKeys: string[];
}

export interface Permission {
  id: string;
  key: string;
  category: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
}

export interface PermissionCategory {
  category: string;
  permissions: Permission[];
}

export interface CreateRoleRequest {
  name: string;
  displayNameEn: string;
  displayNameAr: string;
  description?: string;
}

export interface UpdateRoleRequest {
  id: string;
  displayNameEn: string;
  displayNameAr: string;
  description?: string;
}

/** Data access for roles and the permission catalogue (Security & Administration / Permissions). */
@Injectable({ providedIn: 'root' })
export class RolesService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #rolesUrl = `${this.#config.apiUrl}/api/Roles`;
  readonly #permissionsUrl = `${this.#config.apiUrl}/api/Permissions`;

  list(): Observable<Role[]> {
    return this.#http.get<Role[]>(this.#rolesUrl);
  }

  /** The catalogue is read-only: permissions are declared in code, only grants are editable. */
  permissions(): Observable<PermissionCategory[]> {
    return this.#http.get<PermissionCategory[]>(this.#permissionsUrl);
  }

  create(request: CreateRoleRequest): Observable<string> {
    return this.#http.post<string>(this.#rolesUrl, request);
  }

  update(request: UpdateRoleRequest): Observable<void> {
    return this.#http.put<void>(`${this.#rolesUrl}/${request.id}`, request);
  }

  updatePermissions(roleId: string, permissionIds: string[]): Observable<void> {
    return this.#http.put<void>(`${this.#rolesUrl}/${roleId}/permissions`, { permissionIds });
  }

  delete(id: string): Observable<void> {
    return this.#http.delete<void>(`${this.#rolesUrl}/${id}`);
  }
}
