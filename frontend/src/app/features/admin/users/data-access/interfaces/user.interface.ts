import type { UserType } from '../../../../../core/models/enums';

export interface RoleSummary {
  id: string;
  name: string;
  displayNameEn: string;
  displayNameAr: string;
  description: string | null;
  isSystem: boolean;
}

export interface UserListItem {
  id: string;
  userName: string;
  email: string | null;
  displayNameEn: string;
  displayNameAr: string;
  jobTitle: string | null;
  userType: UserType;
  branchId: string | null;
  branchNameEn: string | null;
  branchNameAr: string | null;
  departmentId: string | null;
  departmentNameEn: string | null;
  departmentNameAr: string | null;
  availabilityStatus: string;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  roles: RoleSummary[];
}

export interface UserDetail extends UserListItem {
  accessibleBranchIds: string[];
  preferredLanguage: string;
  timeZoneId: string | null;
  maxConcurrentTickets: number;
}

export interface UserQuery {
  search?: string;
  roleId?: string;
  departmentId?: string;
  branchId?: string;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDescending?: boolean;
}

export interface CreateUserRequest {
  userName: string;
  email?: string;
  password: string;
  displayNameEn: string;
  displayNameAr: string;
  jobTitle?: string;
  branchId?: string;
  accessibleBranchIds: string[];
  departmentId?: string;
  preferredLanguage: string;
  timeZoneId?: string;
  maxConcurrentTickets: number;
  roleIds: string[];
}

export interface UpdateUserRequest {
  id: string;
  email?: string;
  displayNameEn: string;
  displayNameAr: string;
  jobTitle?: string;
  branchId?: string;
  accessibleBranchIds: string[];
  departmentId?: string;
  preferredLanguage: string;
  timeZoneId?: string;
  maxConcurrentTickets: number;
}

export interface ScopeLookup {
  id: string;
  nameEn: string;
  nameAr: string;
}

export interface UserLookups {
  branches: ScopeLookup[];
  departments: ScopeLookup[];
  roles: RoleSummary[];
}

/** The four states an agent can set on themselves; also the values the capacity check reads. */
export const AVAILABILITY_STATUSES = ['Available', 'Busy', 'Away', 'Offline'] as const;
