import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { RolesService, type Role } from '../../data-access/roles.service';

/**
 * Roles list (Security &amp; Administration / Permissions). System roles are protected from rename
 * and deletion — their names are referenced in code — but their grants stay editable, because an
 * organisation may legitimately want a narrower Agent role.
 */
@Component({
  selector: 'app-role-list',
  imports: [
    FormsModule,
    TranslatePipe,
    PageHeaderComponent,
    EmptyStateComponent,
    ModalComponent,
    HasPermissionDirective,
  ],
  templateUrl: './role-list.page.html',
})
export class RoleListPage {
  readonly #service = inject(RolesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  readonly permissions = PERMISSIONS;

  readonly roles = signal<Role[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);

  readonly formOpen = signal(false);
  readonly editing = signal<Role | null>(null);
  readonly name = signal('');
  readonly displayNameEn = signal('');
  readonly displayNameAr = signal('');
  readonly description = signal('');
  readonly errorMessage = signal<string | null>(null);

  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  roleName(role: Role): string {
    return this.#language.pick({ en: role.displayNameEn, ar: role.displayNameAr });
  }

  openCreate(): void {
    this.editing.set(null);
    this.name.set('');
    this.displayNameEn.set('');
    this.displayNameAr.set('');
    this.description.set('');
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  openRename(role: Role): void {
    this.editing.set(role);
    this.name.set(role.name);
    this.displayNameEn.set(role.displayNameEn);
    this.displayNameAr.set(role.displayNameAr);
    this.description.set(role.description ?? '');
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  editPermissions(role: Role): void {
    void this.#router.navigate(['/admin/roles', role.id, 'permissions']);
  }

  save(): void {
    this.saving.set(true);
    this.errorMessage.set(null);

    const existing = this.editing();

    // Explicitly typed: create() yields Observable<string> and update() Observable<void>, and a bare
    // ternary between them produces a union whose call signatures are not mutually assignable.
    const request$: Observable<string | void> = existing
      ? this.#service.update({
          id: existing.id,
          displayNameEn: this.displayNameEn(),
          displayNameAr: this.displayNameAr(),
          description: this.description() || undefined,
        })
      : this.#service.create({
          name: this.name(),
          displayNameEn: this.displayNameEn(),
          displayNameAr: this.displayNameAr(),
          description: this.description() || undefined,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.#toast.success(existing ? 'admin.roles.updated' : 'admin.roles.created');
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  remove(role: Role): void {
    this.#service.delete(role.id).subscribe({
      next: () => {
        this.#toast.success('admin.roles.deleted');
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
