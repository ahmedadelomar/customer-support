import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AppConfigService } from '../services/app-config.service';
import type { AuthResult, AuthenticatedUser, LoginRequest } from './auth.models';

const TOKEN_KEY = 'cs.token';
const REFRESH_KEY = 'cs.refresh';
const USER_KEY = 'cs.user';

/**
 * Holds the session. State is exposed as signals so guards, the shell and the permission
 * directive all react to sign-in and sign-out without manual subscriptions.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #router = inject(Router);

  readonly #user = signal<AuthenticatedUser | null>(this.#restoreUser());
  readonly #accessToken = signal<string | null>(this.#read(TOKEN_KEY));

  readonly user = this.#user.asReadonly();
  readonly isAuthenticated = computed(() => this.#user() !== null && this.#accessToken() !== null);

  /** Permission set, recomputed once per sign-in rather than on every check. */
  readonly #permissionSet = computed(() => new Set(this.#user()?.permissions ?? []));

  get accessToken(): string | null {
    return this.#accessToken();
  }

  get refreshToken(): string | null {
    return this.#read(REFRESH_KEY);
  }

  login(request: LoginRequest): Observable<AuthResult> {
    return this.#http
      .post<AuthResult>(`${this.#config.apiUrl}/api/auth/login`, request)
      .pipe(tap((result) => this.#applySession(result)));
  }

  refresh(): Observable<AuthResult> {
    return this.#http
      .post<AuthResult>(`${this.#config.apiUrl}/api/auth/refresh`, {
        refreshToken: this.refreshToken,
      })
      .pipe(tap((result) => this.#applySession(result)));
  }

  /**
   * Changes the signed-in user's password. The server returns a fresh session because the old
   * access token still carries `must_change_password`, which the API refuses every other call for.
   */
  changePassword(currentPassword: string, newPassword: string): Observable<AuthResult> {
    return this.#http
      .post<AuthResult>(`${this.#config.apiUrl}/api/auth/change-password`, { currentPassword, newPassword })
      .pipe(tap((result) => this.#applySession(result)));
  }

  logout(redirect = true): void {
    this.#user.set(null);
    this.#accessToken.set(null);

    for (const key of [TOKEN_KEY, REFRESH_KEY, USER_KEY]) {
      try {
        localStorage.removeItem(key);
      } catch {
        // Nothing to clean up if storage is unavailable.
      }
    }

    if (redirect) {
      void this.#router.navigate(['/login']);
    }
  }

  /** True when the signed-in user holds every one of the given permission keys. */
  hasPermission(...keys: string[]): boolean {
    const granted = this.#permissionSet();
    return keys.every((key) => granted.has(key));
  }

  /** True when the user holds at least one of the given keys. Used for menu visibility. */
  hasAnyPermission(...keys: string[]): boolean {
    const granted = this.#permissionSet();
    return keys.some((key) => granted.has(key));
  }

  #applySession(result: AuthResult): void {
    this.#accessToken.set(result.accessToken);
    this.#user.set(result.user);

    try {
      localStorage.setItem(TOKEN_KEY, result.accessToken);
      localStorage.setItem(REFRESH_KEY, result.refreshToken);
      localStorage.setItem(USER_KEY, JSON.stringify(result.user));
    } catch {
      // Session still works in memory for this tab.
    }
  }

  #restoreUser(): AuthenticatedUser | null {
    const raw = this.#read(USER_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as AuthenticatedUser;
    } catch {
      return null;
    }
  }

  #read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }
}
