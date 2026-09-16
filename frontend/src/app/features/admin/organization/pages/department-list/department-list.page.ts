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
import { OrganizationService, type Department } from '../../data-access/organization.service';

/**
 * Departments admin (Platform / Departments, teams and queue scoping).
 *
 * Deactivation is offered but refused server-side while a department still holds open tickets or
 * active teams; that message is surfaced inline rather than as a toast, because it names a number
 * the administrator has to act on.
 */
@Component({
  selector: 'app-department-list',
  imports: [
    FormsModule,
    TranslatePipe,
    PageHeaderComponent,
    EmptyStateComponent,
    ModalComponent,
    HasPermissionDirective,
  ],
  templateUrl: './department-list.page.html',
})
export class DepartmentListPage {
  readonly #service = inject(OrganizationService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly permissions = PERMISSIONS;

  readonly departments = signal<Department[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly formOpen = signal(false);
  readonly editing = signal<Department | null>(null);
  readonly code = signal('');
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly description = signal('');

  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.departments(true).subscribe({
      next: (departments) => {
        this.departments.set(departments);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(department: Department): string {
    return this.#language.pick({ en: department.nameEn, ar: department.nameAr });
  }

  openCreate(): void {
    this.editing.set(null);
    this.code.set('');
    this.nameEn.set('');
    this.nameAr.set('');
    this.description.set('');
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  openEdit(department: Department): void {
    this.editing.set(department);
    this.code.set(department.code);
    this.nameEn.set(department.nameEn);
    this.nameAr.set(department.nameAr);
    this.description.set(department.description ?? '');
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
    const request$: Observable<string | void> = existing
      ? this.#service.updateDepartment(existing.id, {
          nameEn: this.nameEn(),
          nameAr: this.nameAr(),
          description: this.description() || undefined,
        })
      : this.#service.createDepartment({
          code: this.code(),
          nameEn: this.nameEn(),
          nameAr: this.nameAr(),
          description: this.description() || undefined,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.#toast.success(existing ? 'admin.departments.updated' : 'admin.departments.created');
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  setActive(department: Department, isActive: boolean): void {
    this.errorMessage.set(null);

    this.#service.setDepartmentActive(department.id, isActive).subscribe({
      next: () => {
        this.#toast.success(isActive ? 'admin.departments.activated' : 'admin.departments.deactivated');
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
