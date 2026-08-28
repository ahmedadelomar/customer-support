import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../services/toast.service';
import type { ApiProblem } from '../models/api.models';

/**
 * Turns problem-details responses into user-facing toasts.
 *
 * 401 is left alone — `authInterceptor` handles it. 400 validation failures are re-thrown
 * untouched so forms can bind the per-field `errors` map instead of showing a generic toast.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const problem = error.error as ApiProblem | undefined;

      switch (error.status) {
        case 0:
          toast.error('errors.network');
          break;
        case 400:
          // Field errors belong on the form; only a message-less 400 needs a toast.
          if (!problem?.errors) toast.error(problem?.detail ?? 'errors.badRequest');
          break;
        case 401:
          break;
        case 403:
          toast.error('errors.forbidden');
          break;
        case 404:
          toast.error(problem?.detail ?? 'errors.notFound');
          break;
        case 409:
          toast.error(problem?.detail ?? 'errors.conflict');
          break;
        default:
          toast.error(problem?.detail ?? 'errors.unexpected');
      }

      return throwError(() => error);
    }),
  );
};
