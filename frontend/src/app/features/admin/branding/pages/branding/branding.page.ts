import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AppConfigService } from '../../../../../core/services/app-config.service';
import { BrandingService, lighten, type Branding } from '../../../../../core/services/branding.service';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { BranchesService, type Branch } from '../../../organization/data-access/branches.service';

const HEX = /^#[0-9A-Fa-f]{6}$/;

/**
 * Branding admin with a live preview (Platform / Runtime branding and theming).
 *
 * The preview writes the draft colours onto a scoped element rather than `:root`, so previewing an
 * unsaved theme does not restyle the admin screen you are standing on — which would make the
 * controls themselves change colour under you while you drag a picker.
 */
@Component({
  selector: 'app-branding',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent],
  templateUrl: './branding.page.html',
})
export class BrandingPage {
  readonly #branding = inject(BrandingService);
  readonly #branches = inject(BranchesService);
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly errors = signal<Record<string, string[]>>({});

  readonly branches = signal<Branch[]>([]);
  readonly branchId = signal('');
  readonly isBranchOverride = signal(false);

  readonly productNameEn = signal('');
  readonly productNameAr = signal('');
  readonly primaryColor = signal('#5B2C8D');
  readonly secondaryColor = signal('#0E7490');
  readonly logoUrl = signal('');
  readonly supportEmail = signal('');
  readonly supportPhone = signal('');
  readonly portalCustomCss = signal('');

  readonly lang = computed(() => this.#language.current());

  /** Inline style for the preview pane: the draft tokens, scoped to that element only. */
  readonly previewStyle = computed(() => {
    const primary = HEX.test(this.primaryColor()) ? this.primaryColor() : '#5B2C8D';
    const secondary = HEX.test(this.secondaryColor()) ? this.secondaryColor() : '#0E7490';

    return {
      '--brand-700': primary,
      '--brand-600': lighten(primary, 0.1),
      '--brand-100': lighten(primary, 0.85),
      '--brand-50': lighten(primary, 0.94),
      '--accent-600': secondary,
      '--accent-50': lighten(secondary, 0.92),
    } as Record<string, string>;
  });

  readonly previewPrimary = computed(() => this.previewStyle()['--brand-700']);
  readonly previewPrimarySoft = computed(() => this.previewStyle()['--brand-100']);
  readonly previewAccent = computed(() => this.previewStyle()['--accent-600']);

  constructor() {
    this.#branches.list().subscribe({ next: (branches) => this.branches.set(branches) });
    this.load();
  }

  load(): void {
    this.loading.set(true);

    const url = this.branchId()
      ? `${this.#config.apiUrl}/api/Branding?branchId=${this.branchId()}`
      : `${this.#config.apiUrl}/api/Branding`;

    this.#http.get<Branding>(url).subscribe({
      next: (branding) => {
        this.productNameEn.set(branding.productNameEn);
        this.productNameAr.set(branding.productNameAr);
        this.primaryColor.set(branding.primaryColor);
        this.secondaryColor.set(branding.secondaryColor);
        this.logoUrl.set(branding.logoUrl ?? '');
        this.supportEmail.set(branding.supportEmail ?? '');
        this.supportPhone.set(branding.supportPhone ?? '');
        this.portalCustomCss.set(branding.portalCustomCss ?? '');
        this.isBranchOverride.set(branding.isBranchOverride);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  branchName(branch: Branch): string {
    return this.#language.pick({ en: branch.nameEn, ar: branch.nameAr });
  }

  onBranchChange(value: string): void {
    this.branchId.set(value);
    this.load();
  }

  fieldErrors(field: string): string[] {
    return this.errors()[field] ?? [];
  }

  uploadLogo(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const form = new FormData();
    form.append('file', file);

    this.uploading.set(true);
    this.errors.set({});

    this.#http.post(`${this.#config.apiUrl}/api/Branding/logo`, form, { responseType: 'text' }).subscribe({
      next: (url) => {
        this.uploading.set(false);
        this.logoUrl.set(url.replace(/^"|"$/g, ''));
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        this.#captureErrors(error);
      },
    });
  }

  save(): void {
    this.saving.set(true);
    this.errors.set({});

    this.#http
      .put<void>(`${this.#config.apiUrl}/api/Branding`, {
        branchId: this.branchId() || null,
        productNameEn: this.productNameEn(),
        productNameAr: this.productNameAr(),
        primaryColor: this.primaryColor(),
        secondaryColor: this.secondaryColor(),
        logoUrl: this.logoUrl() || null,
        supportEmail: this.supportEmail() || null,
        supportPhone: this.supportPhone() || null,
        portalCustomCss: this.portalCustomCss() || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.#toast.success('admin.branding.saved');

          // Re-apply immediately so the workspace reflects the new theme without a reload.
          void this.#branding.initialise();
          this.load();
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.#captureErrors(error);
        },
      });
  }

  removeOverride(): void {
    const branchId = this.branchId();
    if (!branchId) return;

    this.#http
      .delete<void>(`${this.#config.apiUrl}/api/Branding/override?branchId=${branchId}`)
      .subscribe({
        next: () => {
          this.#toast.success('admin.branding.overrideRemoved');
          this.load();
        },
      });
  }

  #captureErrors(error: unknown): void {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { errors?: Record<string, string[]> } }).error;
      if (problem?.errors) this.errors.set(problem.errors);
    }
  }
}
