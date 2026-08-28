/**
 * Envelope every list endpoint returns. Mirrors `PagedResult<T>` on the server, so the
 * data-table can bind to any feature without per-feature mapping.
 */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** Query-string shape shared by every list request. */
export interface PagedQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

/**
 * RFC 7807 problem details as produced by `GlobalExceptionHandler`.
 * `errors` is present only on validation failures.
 */
export interface ApiProblem {
  status: number;
  title: string;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** Bilingual text pair, matching the `<Prop>En` / `<Prop>Ar` columns on the server. */
export interface LocalizedText {
  en: string;
  ar: string;
}

export type SupportedLanguage = 'ar' | 'en';
