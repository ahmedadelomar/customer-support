import { Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { UsersService } from '../../data-access/users.service';
import type { RoleSummary, ScopeLookup } from '../../data-access/interfaces/user.interface';

/**
 * Create / edit an agent account (Security &amp; Administration / Users and roles).
 *
 * The username is only editable on create: it appears in audit rows and ticket-event actors, and
 * renaming it retroactively rewrites who did what. A new account always starts with a forced
 * password change, so the password field exists on create only.
 */
@Component({
  selector: 'app-user-form',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent],
  templateUrl: './user-form.page.html',
})
export class UserFormPage {
  readonly #service = inject(UsersService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  /** Route parameter: a user id, or `new`. */
  readonly id = input.required<string>();

  readonly isNew = computed(() => this.id() === 'new');
  readonly lang = computed(() => this.#language.current());

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errors = signal<Record<string, string[]>>({});

  readonly branches = signal<ScopeLookup[]>([]);
  readonly departments = signal<ScopeLookup[]>([]);
  readonly roles = signal<RoleSummary[]>([]);

  readonly userName = signal('');
  readonly email = signal('');
  readonly password = signal('');
  readonly displayNameEn = signal('');
  readonly displayNameAr = signal('');
  readonly jobTitle = signal('');
  readonly branchId = signal('');
  readonly departmentId = signal('');
  readonly preferredLanguage = signal('ar');
  readonly maxConcurrentTickets = signal(0);
  readonly roleIds = signal<string[]>([]);

  constructor() {
    queueMicrotask(() => this.#load());
  }

  roleName(role: RoleSummary): string {
    return this.#language.pick({ en: role.displayNameEn, ar: role.displayNameAr });
  }

  scopeName(scope: ScopeLookup): string {
    return this.#language.pick({ en: scope.nameEn, ar: scope.nameAr });
  }

  toggleRole(roleId: string): void {
    this.roleIds.update((current) =>
      current.includes(roleId) ? current.filter((id) => id !== roleId) : [...current, roleId],
    );
  }

  fieldErrors(field: string): string[] {
    return this.errors()[field] ?? [];
  }

  save(): void {
    if (this.roleIds().length === 0) {
      this.errors.set({ roleIds: ['admin.users.validation.roleRequired'] });
      return;
    }

    this.saving.set(true);
    this.errors.set({});

    const request$: Observable<string | void> = this.isNew()
      ? this.#service.create({
          userName: this.userName(),
          email: this.email() || undefined,
          password: this.password(),
          displayNameEn: this.displayNameEn(),
          displayNameAr: this.displayNameAr(),
          jobTitle: this.jobTitle() || undefined,
          branchId: this.branchId() || undefined,
          accessibleBranchIds: [],
          departmentId: this.departmentId() || undefined,
          preferredLanguage: this.preferredLanguage(),
          maxConcurrentTickets: Number(this.maxConcurrentTickets()),
          roleIds: this.roleIds(),
        })
      : this.#service.update({
          id: this.id(),
          email: this.email() || undefined,
          displayNameEn: this.displayNameEn(),
          displayNameAr: this.displayNameAr(),
          jobTitle: this.jobTitle() || undefined,
          branchId: this.branchId() || undefined,
          accessibleBranchIds: [],
          departmentId: this.departmentId() || undefined,
          preferredLanguage: this.preferredLanguage(),
          maxConcurrentTickets: Number(this.maxConcurrentTickets()),
        });

    request$.subscribe({
      next: () => {
        // Roles move through their own endpoint (a different permission), so an edit saves them
        // separately once the profile write has succeeded.
        if (this.isNew()) {
          this.#finish();
          return;
        }

        this.#service.updateRoles(this.id(), this.roleIds()).subscribe({
          next: () => this.#finish(),
          error: (error: unknown) => this.#fail(error),
        });
      },
      error: (error: unknown) => this.#fail(error),
    });
  }

  cancel(): void {
    void this.#router.navigate(['/admin/users']);
  }

  #finish(): void {
    this.saving.set(false);
    this.#toast.success(this.isNew() ? 'admin.users.created' : 'admin.users.updated');
    void this.#router.navigate(['/admin/users']);
  }

  #fail(error: unknown): void {
    this.saving.set(false);

    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { errors?: Record<string, string[]> } }).error;
      if (problem?.errors) this.errors.set(problem.errors);
    }
  }

  #load(): void {
    this.#service.lookups().subscribe({
      next: (lookups) => {
        this.branches.set(lookups.branches);
        this.departments.set(lookups.departments);
        this.roles.set(lookups.roles);
      },
    });

    if (this.isNew()) {
      this.loading.set(false);
      return;
    }

    this.#service.getById(this.id()).subscribe({
      next: (user) => {
        this.userName.set(user.userName);
        this.email.set(user.email ?? '');
        this.displayNameEn.set(user.displayNameEn);
        this.displayNameAr.set(user.displayNameAr);
        this.jobTitle.set(user.jobTitle ?? '');
        this.branchId.set(user.branchId ?? '');
        this.departmentId.set(user.departmentId ?? '');
        this.preferredLanguage.set(user.preferredLanguage);
        this.maxConcurrentTickets.set(user.maxConcurrentTickets);
        this.roleIds.set(user.roles.map((r) => r.id));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
