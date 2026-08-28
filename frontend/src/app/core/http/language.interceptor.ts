import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../services/language.service';

/**
 * Sends the active language on every request so the API can localise validation messages,
 * notification text and exported reports without the caller passing it explicitly.
 */
export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const language = inject(LanguageService);

  return next(
    req.clone({
      setHeaders: { 'Accept-Language': language.current() },
    }),
  );
};
