import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Blocks unauthenticated access and remembers where the user was heading. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Route-level authorisation. Declare the required keys on the route:
 *
 * ```ts
 * { path: 'customers', canActivate: [authGuard, permissionGuard],
 *   data: { permissions: [PERMISSIONS.customers.view] } }
 * ```
 *
 * All listed permissions must be held. This mirrors `AuthorizationBehaviour` on the server;
 * the guard is for navigation only and is never the sole enforcement point.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const required = (route.data?.['permissions'] as string[] | undefined) ?? [];
  if (required.length === 0 || auth.hasPermission(...required)) {
    return true;
  }

  return router.createUrlTree(['/forbidden']);
};
