import { HttpClient } from '@angular/common/http';
import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from './app-config.service';
import { LanguageService } from './language.service';

export interface Branding {
  branchId: string | null;
  productNameEn: string;
  productNameAr: string;
  logoUrl: string | null;
  logoDarkUrl: string | null;
  faviconUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string | null;
  emailHeaderHtml: string | null;
  emailFooterHtml: string | null;
  portalCustomCss: string | null;
  supportEmail: string | null;
  supportPhone: string | null;
  isBranchOverride: boolean;
}

/** Never write a value into a style property without checking it against this first. */
const HEX = /^#[0-9A-Fa-f]{6}$/;

const DEFAULTS: Branding = {
  branchId: null,
  productNameEn: 'Customer Support CRM',
  productNameAr: 'نظام دعم العملاء',
  logoUrl: null,
  logoDarkUrl: null,
  faviconUrl: null,
  primaryColor: '#5B2C8D',
  secondaryColor: '#0E7490',
  accentColor: null,
  emailHeaderHtml: null,
  emailFooterHtml: null,
  portalCustomCss: null,
  supportEmail: null,
  supportPhone: null,
  isBranchOverride: false,
};

/**
 * Applies the tenant theme at runtime (Platform / Runtime branding and theming).
 *
 * Runs during app initialisation, before the first render, so the page never flashes the shipped
 * purple and then repaints in the tenant's colour. The brand tokens are CSS custom properties that
 * Tailwind's `brand-*` scale already reads from, so overwriting them re-themes every component
 * without a rebuild and without touching any component code.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #language = inject(LanguageService);
  readonly #document = inject(DOCUMENT);

  readonly branding = signal<Branding>(DEFAULTS);

  /** Called from `provideAppInitializer`. Never rejects: a themeless app still has to start. */
  async initialise(): Promise<void> {
    try {
      const branding = await firstValueFrom(
        this.#http.get<Branding>(`${this.#config.apiUrl}/api/Branding`),
      );

      this.apply(branding);
    } catch {
      // The API may be unreachable, or this may be a first run with no branding row. The shipped
      // defaults are already in the stylesheet, so there is nothing to undo.
      this.apply(DEFAULTS);
    }
  }

  apply(branding: Branding): void {
    this.branding.set(branding);

    const root = this.#document.documentElement;

    // Validated again here, not only on the server: this string reaches setProperty, and a client
    // that trusts a server response is one compromised endpoint away from CSS injection.
    if (HEX.test(branding.primaryColor)) {
      root.style.setProperty('--brand-700', branding.primaryColor);
      root.style.setProperty('--brand-600', lighten(branding.primaryColor, 0.1));
      root.style.setProperty('--brand-500', lighten(branding.primaryColor, 0.2));
      root.style.setProperty('--brand-100', lighten(branding.primaryColor, 0.85));
      root.style.setProperty('--brand-50', lighten(branding.primaryColor, 0.94));
      root.style.setProperty('--brand-900', lighten(branding.primaryColor, -0.25));
    }

    if (HEX.test(branding.secondaryColor)) {
      root.style.setProperty('--accent-600', branding.secondaryColor);
      root.style.setProperty('--accent-50', lighten(branding.secondaryColor, 0.92));
    }

    this.#applyTitleAndFavicon(branding);
  }

  /** The product name in the active language, for the tab title and the shell header. */
  productName(branding: Branding = this.branding()): string {
    return this.#language.pick({ en: branding.productNameEn, ar: branding.productNameAr });
  }

  #applyTitleAndFavicon(branding: Branding): void {
    this.#document.title = this.productName(branding);

    if (!branding.faviconUrl) {
      return;
    }

    const link =
      this.#document.querySelector<HTMLLinkElement>("link[rel='icon']") ??
      this.#document.createElement('link');

    link.rel = 'icon';
    link.href = branding.faviconUrl;

    if (!link.parentNode) {
      this.#document.head.appendChild(link);
    }
  }
}

/**
 * Mixes a hex colour towards white (positive amount) or black (negative), returning hex.
 * Enough to derive a usable tint ramp from one brand colour; it is not a colour-science model.
 */
export function lighten(hex: string, amount: number): string {
  const value = hex.replace('#', '');
  const target = amount >= 0 ? 255 : 0;
  const ratio = Math.abs(amount);

  const channel = (offset: number) => {
    const base = parseInt(value.slice(offset, offset + 2), 16);
    return Math.round(base + (target - base) * ratio)
      .toString(16)
      .padStart(2, '0');
  };

  return `#${channel(0)}${channel(2)}${channel(4)}`;
}
