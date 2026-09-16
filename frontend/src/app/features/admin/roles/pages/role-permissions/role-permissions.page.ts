import { Component, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { AppConfigService } from '../../../../../core/services/app-config.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { RolesService, type Permission, type PermissionCategory, type Role } from '../../data-access/roles.service';

/**
 * Permission matrix for one role (Security &amp; Administration / Permissions).
 *
 * Grants are carried in a local set and only sent on save, so the Save button can stay disabled
 * until something actually changed. The screen states the refresh delay plainly — a permission
 * change that appears to do nothing for an hour otherwise reads as a bug rather than as design.
 */
@Component({
  selector: 'app-role-permissions',
  imports: [TranslatePipe, PageHeaderComponent],
  templateUrl: './role-permissions.page.html',
})
export class RolePermissionsPage {
  readonly #service = inject(RolesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #config = inject(AppConfigService);
  readonly #router = inject(Router);

  /** Route parameter: the role being edited. */
  readonly id = input.required<string>();

  readonly categories = signal<PermissionCategory[]>([]);
  readonly role = signal<Role | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly collapsed = signal<ReadonlySet<string>>(new Set());
  readonly granted = signal<ReadonlySet<string>>(new Set());
  readonly #original = signal<ReadonlySet<string>>(new Set());

  readonly lang = computed(() => this.#language.current());

  /** Sessions refresh on the access-token lifetime, so that is the delay the notice must quote. */
  readonly accessTokenMinutes = 60;

  readonly dirty = computed(() => {
    const current = this.granted();
    const original = this.#original();
    return current.size !== original.size || [...current].some((id) => !original.has(id));
  });

  constructor() {
    queueMicrotask(() => this.#load());
  }

  roleName(): string {
    const role = this.role();
    if (!role) return '';
    return this.#language.pick({ en: role.displayNameEn, ar: role.displayNameAr });
  }

  permissionName(permission: Permission): string {
    return this.#language.pick({ en: permission.nameEn, ar: permission.nameAr });
  }

  isGranted(permissionId: string): boolean {
    return this.granted().has(permissionId);
  }

  toggle(permissionId: string): void {
    this.granted.update((current) => {
      const next = new Set(current);
      if (next.has(permissionId)) next.delete(permissionId);
      else next.add(permissionId);
      return next;
    });
  }

  isCollapsed(category: string): boolean {
    return this.collapsed().has(category);
  }

  toggleCategory(category: string): void {
    this.collapsed.update((current) => {
      const next = new Set(current);
      if (next.has(category)) next.delete(category);
      else next.add(category);
      return next;
    });
  }

  grantedInCategory(category: PermissionCategory): number {
    return category.permissions.filter((p) => this.granted().has(p.id)).length;
  }

  allInCategory(category: PermissionCategory): boolean {
    return category.permissions.length > 0 && this.grantedInCategory(category) === category.permissions.length;
  }

  /** Header checkbox: grants the whole category, or clears it when everything is already granted. */
  toggleAllInCategory(category: PermissionCategory): void {
    const grantAll = !this.allInCategory(category);

    this.granted.update((current) => {
      const next = new Set(current);
      for (const permission of category.permissions) {
        if (grantAll) next.add(permission.id);
        else next.delete(permission.id);
      }
      return next;
    });
  }

  save(): void {
    this.saving.set(true);
    this.errorMessage.set(null);

    this.#service.updatePermissions(this.id(), [...this.granted()]).subscribe({
      next: () => {
        this.saving.set(false);
        this.#original.set(new Set(this.granted()));
        this.#toast.success('admin.roles.permissionsSaved');
      },
      error: (error: unknown) => {
        this.saving.set(false);

        // The self-lockout guard returns a 409 the administrator genuinely needs to read, so it is
        // shown inline above the matrix rather than as a toast that disappears.
        if (error && typeof error === 'object' && 'error' in error) {
          const problem = (error as { error?: { detail?: string } }).error;
          this.errorMessage.set(problem?.detail ?? null);
        }
      },
    });
  }

  back(): void {
    void this.#router.navigate(['/admin/roles']);
  }

  #load(): void {
    this.#service.permissions().subscribe({
      next: (categories) => this.categories.set(categories),
    });

    this.#service.list().subscribe({
      next: (roles) => {
        const role = roles.find((r) => r.id === this.id()) ?? null;
        this.role.set(role);
        this.granted.set(new Set(role?.permissionIds ?? []));
        this.#original.set(new Set(role?.permissionIds ?? []));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
