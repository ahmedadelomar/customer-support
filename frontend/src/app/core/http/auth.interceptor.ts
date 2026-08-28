import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/** Shared across concurrent 401s so one refresh serves every queued request. */
let refreshing = false;
const refreshed = new BehaviorSubject<string | null>(null);

/**
 * Attaches the bearer token and transparently refreshes it once on a 401.
 *
 * Requests that fire while a refresh is in flight wait for its result rather than each
 * triggering their own refresh, which would invalidate the token repeatedly.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  // The auth endpoints must not carry a stale token, or refresh would fail on an expired one.
  const isAuthEndpoint = req.url.includes('/api/auth/');
  const token = auth.accessToken;

  const authorised =
    token && !isAuthEndpoint
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authorised).pipe(
    catchError((error: unknown) => {
      const is401 = error instanceof HttpErrorResponse && error.status === 401;
      if (!is401 || isAuthEndpoint || !auth.refreshToken) {
        return throwError(() => error);
      }

      if (refreshing) {
        return refreshed.pipe(
          filter((value): value is string => value !== null),
          take(1),
          switchMap((fresh) =>
            next(req.clone({ setHeaders: { Authorization: `Bearer ${fresh}` } })),
          ),
        );
      }

      refreshing = true;
      refreshed.next(null);

      return auth.refresh().pipe(
        switchMap((result) => {
          refreshing = false;
          refreshed.next(result.accessToken);
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${result.accessToken}` } }));
        }),
        catchError((refreshError: unknown) => {
          refreshing = false;
          auth.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};
