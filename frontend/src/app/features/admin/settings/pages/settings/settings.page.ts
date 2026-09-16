import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../../../core/auth/auth.service';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import {
  SettingSource,
  SettingsService,
  type Setting,
  type SettingCategory,
} from '../../data-access/settings.service';

/**
 * System configuration screen (Security &amp; Administration / System configuration).
 *
 * Saves per section rather than per field, so a related group of changes lands together. The editor
 * is chosen from `dataType`, and each setting shows where its value came from — a branch override
 * that silently shadows a global value is exactly the kind of thing that wastes an afternoon.
 */
@Component({
  selector: 'app-settings',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent],
  templateUrl: './settings.page.html',
})
export class SettingsPage {
  readonly #service = inject(SettingsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #auth = inject(AuthService);

  readonly SettingSource = SettingSource;

  /**
   * The screen resolves settings for the caller's own branch (the server's default when no branch is
   * passed), so this is the only branch whose overrides it can offer to remove. Editing another
   * branch's overrides needs a branch picker, which this screen does not have yet.
   */
  readonly currentBranchId = computed(() => this.#auth.user()?.branchId ?? null);

  readonly categories = signal<SettingCategory[]>([]);
  readonly loading = signal(true);
  readonly savingCategory = signal<string | null>(null);
  readonly errors = signal<Record<string, string>>({});

  /** Edited values keyed by setting key; a key is present only once the field has been touched. */
  readonly drafts = signal<Record<string, string>>({});

  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.drafts.set({});

    this.#service.list().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(setting: Setting): string {
    return this.#language.pick({ en: setting.nameEn, ar: setting.nameAr });
  }

  description(setting: Setting): string {
    return this.#language.pick({
      en: setting.descriptionEn ?? '',
      ar: setting.descriptionAr ?? '',
    });
  }

  value(setting: Setting): string {
    return this.drafts()[setting.key] ?? setting.value ?? '';
  }

  boolValue(setting: Setting): boolean {
    return this.value(setting) === 'true';
  }

  setValue(setting: Setting, value: string): void {
    this.drafts.update((current) => ({ ...current, [setting.key]: value }));
  }

  setBool(setting: Setting, value: boolean): void {
    this.setValue(setting, value ? 'true' : 'false');
  }

  error(key: string): string | null {
    return this.errors()[key] ?? null;
  }

  /** A section is dirty when any of its settings has a draft that differs from the stored value. */
  isDirty(category: SettingCategory): boolean {
    const drafts = this.drafts();
    return category.settings.some(
      (s) => drafts[s.key] !== undefined && drafts[s.key] !== (s.value ?? ''),
    );
  }

  sourceLabelKey(source: SettingSource): string {
    switch (source) {
      case SettingSource.Branch:
        return 'admin.settings.source.branch';
      case SettingSource.Global:
        return 'admin.settings.source.global';
      default:
        return 'admin.settings.source.default';
    }
  }

  save(category: SettingCategory): void {
    const drafts = this.drafts();
    const changed = category.settings.filter(
      (s) => drafts[s.key] !== undefined && drafts[s.key] !== (s.value ?? ''),
    );

    if (changed.length === 0) {
      return;
    }

    this.savingCategory.set(category.category);
    this.errors.set({});

    forkJoin(
      changed.map((s) => this.#service.update(s.key, drafts[s.key])),
    ).subscribe({
      next: () => {
        this.savingCategory.set(null);
        this.#toast.success('admin.settings.saved');
        this.load();
      },
      error: (error: unknown) => {
        this.savingCategory.set(null);

        // The server validates each value against its declared type; surface that on the field.
        const message = this.#extractMessage(error);
        if (message) {
          this.errors.set(Object.fromEntries(changed.map((s) => [s.key, message])));
        }
      },
    });
  }

  removeOverride(setting: Setting, branchId: string): void {
    this.#service.removeOverride(setting.key, branchId).subscribe({
      next: () => {
        this.#toast.success('admin.settings.overrideRemoved');
        this.load();
      },
    });
  }

  #extractMessage(error: unknown): string | null {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { detail?: string; errors?: Record<string, string[]> } }).error;
      if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
      if (problem?.detail) return problem.detail;
    }
    return null;
  }
}
