import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';

export interface Department {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  description: string | null;
  managerId: string | null;
  managerNameEn: string | null;
  managerNameAr: string | null;
  branchId: string | null;
  isActive: boolean;
  teamCount: number;
  openTicketCount: number;
}

export interface TeamMember {
  userId: string;
  nameEn: string;
  nameAr: string;
  isLead: boolean;
  maxConcurrentTickets: number;
  rotationOrder: number;
  isActive: boolean;
}

export interface Team {
  id: string;
  departmentId: string;
  departmentNameEn: string;
  departmentNameAr: string;
  nameEn: string;
  nameAr: string;
  leadUserId: string | null;
  isActive: boolean;
  members: TeamMember[];
}

export interface TeamMemberInput {
  userId: string;
  maxConcurrentTickets: number;
  isLead: boolean;
}

/** Data access for departments and teams (Platform / Departments, teams and queue scoping). */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #departmentsUrl = `${this.#config.apiUrl}/api/Departments`;
  readonly #teamsUrl = `${this.#config.apiUrl}/api/Teams`;

  departments(includeInactive = false): Observable<Department[]> {
    return this.#http.get<Department[]>(this.#departmentsUrl, {
      params: new HttpParams().set('includeInactive', includeInactive),
    });
  }

  createDepartment(request: {
    code: string;
    nameEn: string;
    nameAr: string;
    description?: string;
    managerId?: string;
  }): Observable<string> {
    return this.#http.post<string>(this.#departmentsUrl, request);
  }

  updateDepartment(
    id: string,
    request: { nameEn: string; nameAr: string; description?: string; managerId?: string },
  ): Observable<void> {
    return this.#http.put<void>(`${this.#departmentsUrl}/${id}`, request);
  }

  setDepartmentActive(id: string, isActive: boolean): Observable<void> {
    return this.#http.post<void>(
      `${this.#departmentsUrl}/${id}/${isActive ? 'activate' : 'deactivate'}`,
      {},
    );
  }

  teams(departmentId?: string, includeInactive = false): Observable<Team[]> {
    let params = new HttpParams().set('includeInactive', includeInactive);
    if (departmentId) params = params.set('departmentId', departmentId);

    return this.#http.get<Team[]>(this.#teamsUrl, { params });
  }

  createTeam(request: {
    departmentId: string;
    nameEn: string;
    nameAr: string;
    leadUserId?: string;
  }): Observable<string> {
    return this.#http.post<string>(this.#teamsUrl, request);
  }

  updateTeam(
    id: string,
    request: { nameEn: string; nameAr: string; leadUserId?: string; isActive: boolean },
  ): Observable<void> {
    return this.#http.put<void>(`${this.#teamsUrl}/${id}`, request);
  }

  /** Replaces membership; the array order becomes the round-robin rotation order. */
  updateMembers(teamId: string, members: TeamMemberInput[]): Observable<void> {
    return this.#http.put<void>(`${this.#teamsUrl}/${teamId}/members`, { members });
  }
}
