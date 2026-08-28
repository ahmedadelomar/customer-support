import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import type { SupportedLanguage } from '../models/api.models';

const STORAGE_KEY = 'cs.lang';

/**
 * Owns the active language and document direction (Platform / Arabic and English).
 *
 * Arabic is the default, and direction is applied to `<html>` so Tailwind logical properties
 * mirror the whole UI without a second stylesheet.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly #translate = inject(TranslateService);
  readonly #document = inject(DOCUMENT);
  readonly #isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly #current = signal<SupportedLanguage>('ar');

  /** Active language. Components depend on this inside `computed` to re-render on switch. */
  readonly current = this.#current.asReadonly();

  readonly direction = computed<'rtl' | 'ltr'>(() => (this.#current() === 'ar' ? 'rtl' : 'ltr'));
  readonly isRtl = computed(() => this.direction() === 'rtl');

  /** BCP-47 locale for `Intl` formatting of dates and numbers. */
  readonly locale = computed(() => (this.#current() === 'ar' ? 'ar-SA' : 'en-US'));

  constructor() {
    this.#translate.addLangs(['ar', 'en']);
    this.#translate.setFallbackLang('ar');

    // Applying direction in an effect keeps <html> in sync no matter who changes the signal.
    effect(() => {
      const lang = this.#current();
      const html = this.#document.documentElement;
      html.setAttribute('lang', lang);
      html.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
    });
  }

  /** Called once during bootstrap, before the first render, to avoid a flash of the wrong direction. */
  initialise(): void {
    // On the server there is no stored preference and no origin to resolve the translation URL
    // against, so set the language without triggering a fetch. Every route renders on the
    // client (see `app.routes.server.ts`), which is where translations actually load.
    if (!this.#isBrowser) {
      this.#current.set('ar');
      return;
    }

    this.use(this.#readStoredLanguage() ?? this.#detectBrowserLanguage());
  }

  use(lang: SupportedLanguage): void {
    this.#current.set(lang);

    if (!this.#isBrowser) {
      return;
    }

    this.#translate.use(lang);

    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // Private browsing can block storage; the language still applies for this session.
    }
  }

  toggle(): void {
    this.use(this.#current() === 'ar' ? 'en' : 'ar');
  }

  /** Picks the right half of a bilingual pair returned by the API. */
  pick(text: { en: string; ar: string } | null | undefined): string {
    if (!text) return '';
    return this.#current() === 'ar' ? text.ar || text.en : text.en || text.ar;
  }

  #readStoredLanguage(): SupportedLanguage | null {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored === 'ar' || stored === 'en' ? stored : null;
    } catch {
      return null;
    }
  }

  #detectBrowserLanguage(): SupportedLanguage {
    const browser = this.#translate.getBrowserLang();
    return browser === 'en' ? 'en' : 'ar';
  }
}
