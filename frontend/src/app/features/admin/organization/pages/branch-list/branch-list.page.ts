import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { BranchesService, TIME_ZONES, type Branch } from '../../data-access/branches.service';

/**
 * Branches admin (Platform / Branch scoping and the branch switcher).
 *
 * Deactivation is refused server-side while active users or open tickets remain; that message names
 * a count the administrator has to act on, so it is shown inline rather than as a toast.
 */
@Component({
  selector: 'app-branch-list',
  imports: [
    FormsModule,
    TranslatePipe,
    PageHeaderComponent,
    EmptyStateComponent,
    ModalComponent,
    HasPermissionDirective,
  ],
  templateUrl: './branch-list.page.html',
})
export class BranchListPage {
  readonly #service = inject(BranchesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly permissions = PERMISSIONS;
  readonly timeZones = TIME_ZONES;

  readonly branches = signal<Branch[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly formOpen = signal(false);
  readonly editing = signal<Branch | null>(null);
  readonly code = signal('');
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly timeZoneId = signal<string>('Asia/Riyadh');
  readonly address = signal('');
  readonly phoneNumber = signal('');

  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list(true).subscribe({
      next: (branches) => {
        this.branches.set(branches);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(branch: Branch): string {
    return this.#language.pick({ en: branch.nameEn, ar: branch.nameAr });
  }

  openCreate(): void {
    this.editing.set(null);
    this.code.set('');
    this.nameEn.set('');
    this.nameAr.set('');
    this.timeZoneId.set('Asia/Riyadh');
    this.address.set('');
    this.phoneNumber.set('');
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  openEdit(branch: Branch): void {
    this.editing.set(branch);
    this.code.set(branch.code);
    this.nameEn.set(branch.nameEn);
    this.nameAr.set(branch.nameAr);
    this.timeZoneId.set(branch.timeZoneId ?? 'Asia/Riyadh');
    this.address.set(branch.address ?? '');
    this.phoneNumber.set(branch.phoneNumber ?? '');
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    this.saving.set(true);
    this.errorMessage.set(null);

    const existing = this.editing();
    const payload = {
      nameEn: this.nameEn(),
      nameAr: this.nameAr(),
      timeZoneId: this.timeZoneId(),
      address: this.address() || undefined,
      phoneNumber: this.phoneNumber() || undefined,
    };

    const request$: Observable<string | void> = existing
      ? this.#service.update(existing.id, payload)
      : this.#service.create({ code: this.code(), ...payload });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.#toast.success(existing ? 'admin.branches.updated' : 'admin.branches.created');
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  setActive(branch: Branch, isActive: boolean): void {
    this.errorMessage.set(null);

    this.#service.setActive(branch.id, isActive).subscribe({
      next: () => {
        this.#toast.success(isActive ? 'admin.branches.activated' : 'admin.branches.deactivated');
        this.load();
      },
      error: (error: unknown) => this.errorMessage.set(this.#extractMessage(error)),
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
