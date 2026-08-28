import type { SupportedLanguage } from '../models/api.models';

/** Response from `POST /api/auth/login` and `POST /api/auth/refresh`. */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

export interface AuthenticatedUser {
  id: string;
  userName: string;
  email: string;
  displayNameEn: string;
  displayNameAr: string;
  avatarUrl?: string;
  userType: number;
  branchId?: string;
  accessibleBranchIds: string[];
  departmentId?: string;
  customerId?: string;
  preferredLanguage: SupportedLanguage;
  roles: string[];
  /** Permission keys, matching `PERMISSIONS` in `core/permissions.ts`. */
  permissions: string[];
  mustChangePassword: boolean;
}

export interface LoginRequest {
  userName: string;
  password: string;
}
