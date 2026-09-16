import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { PERMISSIONS } from '../../../../../core/permissions';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { DataTableComponent } from '../../../../../shared/ui/data-table/data-table.component';
import type {
  DataTableColumn,
  SortState,
  StatusChipConfigMap,
} from '../../../../../shared/ui/data-table/data-table.models';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { PaginationComponent } from '../../../../../shared/ui/pagination/pagination.component';
import { SearchInputComponent } from '../../../../../shared/ui/search-input/search-input.component';
import { UsersService } from '../../data-access/users.service';
import type { RoleSummary, ScopeLookup, UserListItem } from '../../data-access/interfaces/user.interface';

/**
 * User administration list (Security &amp; Administration / Users and roles).
 *
 * There is deliberately no delete action: tickets, ticket events and audit rows reference the user
 * id permanently, so the product rule is deactivate, never delete.
 */
@Component({
  selector: 'app-user-list',
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    DataTableComponent,
    PageHeaderComponent,
    PaginationComponent,
    SearchInputComponent,
    HasPermissionDirective,
  ],
  templateUrl: './user-list.page.html',
})
export class UserListPage implements OnInit {
  readonly #service = inject(UsersService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  readonly permissions = PERMISSIONS;

  readonly rows = signal<UserListItem[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly sort = signal<SortState>({ sortBy: 'displayName', sortDescending: false });

  readonly searchTerm = signal('');
  readonly roleFilter = signal('');
  readonly departmentFilter = signal('');
  readonly activeFilter = signal('');

  readonly roles = signal<RoleSummary[]>([]);
  readonly departments = signal<ScopeLookup[]>([]);

  readonly lang = computed(() => this.#language.current());

  /** Availability drives the chip colour, matching the states the capacity check reads. */
  readonly statusConfig: StatusChipConfigMap = {
    Available: {
      labelKey: 'admin.users.availability.available',
      chipClasses: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      dotClasses: 'bg-emerald-500',
    },
    Busy: {
      labelKey: 'admin.users.availability.busy',
      chipClasses: 'border-amber-200 bg-amber-50 text-amber-700',
      dotClasses: 'bg-amber-500',
    },
    Away: {
      labelKey: 'admin.users.availability.away',
      chipClasses: 'border-slate-200 bg-slate-50 text-slate-600',
      dotClasses: 'bg-slate-400',
    },
    Offline: {
      labelKey: 'admin.users.availability.offline',
      chipClasses: 'border-slate-200 bg-white text-slate-500',
      dotClasses: 'bg-slate-300',
    },
  };

  readonly columns = computed<DataTableColumn<UserListItem>[]>(() => [
    {
      id: 'displayName',
      headerKey: 'admin.users.columns.name',
      type: 'text',
      sortable: true,
      value: (row) => this.#language.pick({ en: row.displayNameEn, ar: row.displayNameAr }),
    },
    { id: 'userName', headerKey: 'admin.users.columns.userName', type: 'text', field: 'userName', sortable: true },
    { id: 'email', headerKey: 'admin.users.columns.email', type: 'text', field: 'email', hideOnMobile: true },
    {
      id: 'roles',
      headerKey: 'admin.users.columns.roles',
      type: 'text',
      hideOnMobile: true,
      value: (row) =>
        row.roles
          .map((r) => this.#language.pick({ en: r.displayNameEn, ar: r.displayNameAr }))
          .join(', '),
    },
    {
      id: 'department',
      headerKey: 'admin.users.columns.department',
      type: 'text',
      hideOnMobile: true,
      value: (row) =>
        row.departmentId
          ? this.#language.pick({ en: row.departmentNameEn ?? '', ar: row.departmentNameAr ?? '' })
          : '—',
    },
    {
      id: 'branch',
      headerKey: 'admin.users.columns.branch',
      type: 'text',
      hideOnMobile: true,
      value: (row) =>
        row.branchId
          ? this.#language.pick({ en: row.branchNameEn ?? '', ar: row.branchNameAr ?? '' })
          : '—',
    },
    { id: 'availabilityStatus', headerKey: 'admin.users.columns.availability', type: 'status', field: 'availabilityStatus' },
    {
      id: 'isActive',
      headerKey: 'admin.users.columns.status',
      type: 'text',
      value: (row) => (row.isActive ? '●' : '○'),
    },
    {
      id: 'actions',
      headerKey: '',
      type: 'actions',
      alignEnd: true,
      actions: [
        {
          labelKey: 'common.edit',
          icon: '✎',
          visible: () => true,
          run: (row) => void this.#router.navigate(['/admin/users', row.id]),
        },
        {
          labelKey: 'admin.users.actions.deactivate',
          icon: '⊘',
          danger: true,
          visible: (row) => row.isActive,
          run: (row) => this.setActive(row, false),
        },
        {
          labelKey: 'admin.users.actions.activate',
          icon: '✓',
          visible: (row) => !row.isActive,
          run: (row) => this.setActive(row, true),
        },
      ],
    },
  ]);

  ngOnInit(): void {
    this.#service.lookups().subscribe({
      next: (lookups) => {
        this.roles.set(lookups.roles);
        this.departments.set(lookups.departments);
      },
    });

    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.#service
      .list({
        search: this.searchTerm() || undefined,
        roleId: this.roleFilter() || undefined,
        departmentId: this.departmentFilter() || undefined,
        isActive: this.activeFilter() === '' ? undefined : this.activeFilter() === 'true',
        page: this.page(),
        pageSize: this.pageSize(),
        sortBy: this.sort().sortBy,
        sortDescending: this.sort().sortDescending,
      })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  scopeName(scope: ScopeLookup): string {
    return this.#language.pick({ en: scope.nameEn, ar: scope.nameAr });
  }

  roleName(role: RoleSummary): string {
    return this.#language.pick({ en: role.displayNameEn, ar: role.displayNameAr });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
    this.load();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  onSortChange(sort: SortState): void {
    this.sort.set(sort);
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  setActive(row: UserListItem, isActive: boolean): void {
    const request$ = isActive ? this.#service.activate(row.id) : this.#service.deactivate(row.id);

    request$.subscribe({
      next: () => {
        this.#toast.success(isActive ? 'admin.users.activated' : 'admin.users.deactivated');
        this.load();
      },
    });
  }
}
