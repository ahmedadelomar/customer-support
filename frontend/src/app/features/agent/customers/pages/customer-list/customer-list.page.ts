import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../../../core/auth/auth.service';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { CustomerType } from '../../../../../core/models/enums';
import { PERMISSIONS } from '../../../../../core/permissions';
import { AppConfigService } from '../../../../../core/services/app-config.service';
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
import { StateCardComponent } from '../../../../../shared/ui/state-card/state-card.component';
import { CustomersService } from '../../data-access/customers.service';
import type { CustomerListItem, CustomerQuery } from '../../data-access/interfaces/customer.interface';

/**
 * Customer list (Customer Management / Customer profiles).
 *
 * This page is the reference pattern for every list screen in the app:
 *  - all filter state lives in the URL, so a filtered view is shareable and survives a refresh;
 *  - columns are `computed` so they re-render with translated headers on a language switch;
 *  - the component owns loading state as signals — required, since the app is zoneless.
 */
@Component({
  selector: 'app-customer-list',
  imports: [
    FormsModule,
    RouterLink,
    TranslatePipe,
    DataTableComponent,
    PageHeaderComponent,
    PaginationComponent,
    SearchInputComponent,
    StateCardComponent,
    HasPermissionDirective,
  ],
  templateUrl: './customer-list.page.html',
})
export class CustomerListPage implements OnInit {
  readonly #service = inject(CustomersService);
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);
  readonly #toast = inject(ToastService);
  readonly #config = inject(AppConfigService);
  readonly #auth = inject(AuthService);

  readonly permissions = PERMISSIONS;

  // --- State ------------------------------------------------------------------------------
  readonly items = signal<CustomerListItem[]>([]);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(this.#config.defaultPageSize);
  readonly totalCount = signal(0);
  readonly sort = signal<SortState | null>(null);

  readonly searchTerm = signal('');
  readonly typeFilter = signal<string>('');
  readonly statusFilter = signal<string>('');
  readonly openTicketsFilter = signal<string>('');

  readonly activeCount = signal(0);
  readonly withOpenTicketsCount = signal(0);

  readonly typeOptions = [
    { value: '', labelKey: 'customers.filters.allTypes' },
    { value: String(CustomerType.Individual), labelKey: 'enums.customerType.0' },
    { value: String(CustomerType.Company), labelKey: 'enums.customerType.1' },
    { value: String(CustomerType.Government), labelKey: 'enums.customerType.2' },
  ];

  readonly statusOptions = [
    { value: '', labelKey: 'customers.filters.allStatuses' },
    { value: 'active', labelKey: 'customers.status.active' },
    { value: 'inactive', labelKey: 'customers.status.inactive' },
    { value: 'blocked', labelKey: 'customers.status.blocked' },
  ];

  /**
   * Chip styling per derived status. Built here rather than in the table so the same
   * vocabulary is reused by the detail page.
   */
  readonly statusConfig = computed<StatusChipConfigMap>(() => ({
    active: {
      labelKey: 'customers.status.active',
      chipClasses: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      dotClasses: 'bg-emerald-500',
    },
    inactive: {
      labelKey: 'customers.status.inactive',
      chipClasses: 'border-slate-200 bg-slate-100 text-slate-600',
      dotClasses: 'bg-slate-400',
    },
    blocked: {
      labelKey: 'customers.status.blocked',
      chipClasses: 'border-rose-200 bg-rose-50 text-rose-700',
      dotClasses: 'bg-rose-500',
    },
  }));

  /**
   * Columns depend on the active language so a switch re-renders headers and action labels.
   * Reading `this.#language.current()` inside the computed is what creates that dependency.
   */
  readonly columns = computed<DataTableColumn<CustomerListItem>[]>(() => {
    const lang = this.#language.current();
    const canUpdate = this.#auth.hasPermission(PERMISSIONS.customers.update);
    const canDelete = this.#auth.hasPermission(PERMISSIONS.customers.delete);

    return [
      { id: 'code', headerKey: 'customers.columns.code', type: 'text', sortable: true },
      {
        id: 'displayName',
        headerKey: 'customers.columns.name',
        type: 'text',
        sortable: true,
        value: (row) => (lang === 'ar' ? row.displayNameAr || row.displayNameEn : row.displayNameEn || row.displayNameAr),
      },
      {
        id: 'type',
        headerKey: 'customers.columns.type',
        type: 'text',
        hideOnMobile: true,
        value: (row) => this.#translate.instant(`enums.customerType.${row.type}`),
      },
      { id: 'primaryEmail', headerKey: 'customers.columns.email', type: 'text', hideOnMobile: true },
      { id: 'primaryPhone', headerKey: 'customers.columns.phone', type: 'text' },
      {
        id: 'openTicketCount',
        headerKey: 'customers.columns.openTickets',
        type: 'number',
        alignEnd: true,
      },
      {
        id: 'status',
        headerKey: 'customers.columns.status',
        type: 'status',
        value: (row) => this.#derivedStatus(row),
      },
      {
        id: 'lastInteractionAt',
        headerKey: 'customers.columns.lastInteraction',
        type: 'date',
        sortable: true,
        hideOnMobile: true,
      },
      {
        id: 'actions',
        headerKey: '',
        type: 'actions',
        actions: [
          {
            labelKey: 'common.view',
            run: (row) => void this.#router.navigate(['/agent/customers', row.id]),
          },
          {
            labelKey: 'common.edit',
            visible: () => canUpdate,
            run: (row) => void this.#router.navigate(['/agent/customers', row.id, 'edit']),
          },
          {
            labelKey: 'common.delete',
            danger: true,
            // Deleting is refused server-side while open tickets exist; disable it here too
            // so the agent gets the answer without a round trip.
            visible: () => canDelete,
            disabled: (row) => row.openTicketCount > 0,
            run: (row) => this.confirmDelete(row),
          },
        ],
      },
    ];
  });

  ngOnInit(): void {
    // Query params are the source of truth: back/forward and a hard refresh both restore the view.
    this.#route.queryParamMap.subscribe((params) => {
      this.page.set(Number(params.get('page')) || 1);
      this.pageSize.set(Number(params.get('pageSize')) || this.#config.defaultPageSize);
      this.searchTerm.set(params.get('search') ?? '');
      this.typeFilter.set(params.get('type') ?? '');
      this.statusFilter.set(params.get('status') ?? '');
      this.openTicketsFilter.set(params.get('hasOpenTickets') ?? '');

      const sortBy = params.get('sortBy');
      this.sort.set(sortBy ? { sortBy, sortDescending: params.get('desc') === 'true' } : null);

      this.load();
    });
  }

  load(): void {
    this.loading.set(true);

    const query: CustomerQuery = {
      page: this.page(),
      pageSize: this.pageSize(),
      search: this.searchTerm() || undefined,
      type: this.typeFilter() ? (Number(this.typeFilter()) as CustomerType) : undefined,
      sortBy: this.sort()?.sortBy,
      sortDescending: this.sort()?.sortDescending,
      ...this.#statusToQuery(this.statusFilter()),
      hasOpenTickets: this.openTicketsFilter() ? this.openTicketsFilter() === 'true' : undefined,
    };

    this.#service.list(query).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.activeCount.set(result.items.filter((c) => c.isActive && !c.isBlocked).length);
        this.withOpenTicketsCount.set(result.items.filter((c) => c.openTicketCount > 0).length);
        this.loading.set(false);
      },
      // The error interceptor already raised a toast; just release the loading state.
      error: () => this.loading.set(false),
    });
  }

  // --- Filter handlers: every one writes to the URL, which re-triggers `load()` ---------------
  onSearch(term: string): void {
    this.#patchQuery({ search: term || null, page: 1 });
  }

  onFilterChange(): void {
    this.#patchQuery({
      type: this.typeFilter() || null,
      status: this.statusFilter() || null,
      hasOpenTickets: this.openTicketsFilter() || null,
      page: 1,
    });
  }

  onSortChange(sort: SortState): void {
    this.#patchQuery({ sortBy: sort.sortBy, desc: String(sort.sortDescending), page: 1 });
  }

  onPageChange(page: number): void {
    this.#patchQuery({ page });
  }

  clearFilters(): void {
    this.#router.navigate([], { relativeTo: this.#route, queryParams: {} });
  }

  openCustomer(row: CustomerListItem): void {
    void this.#router.navigate(['/agent/customers', row.id]);
  }

  confirmDelete(row: CustomerListItem): void {
    const name = this.#language.pick({ en: row.displayNameEn, ar: row.displayNameAr });
    if (!confirm(this.#translate.instant('customers.confirmDelete', { name }))) {
      return;
    }

    this.#service.delete(row.id).subscribe({
      next: () => {
        this.#toast.success('customers.deleted');
        this.load();
      },
      error: () => {
        // Conflict detail is surfaced by the interceptor; nothing further to do here.
      },
    });
  }

  /** Collapses the active/blocked flags into the single status the chip renders. */
  #derivedStatus(row: CustomerListItem): string {
    if (row.isBlocked) return 'blocked';
    return row.isActive ? 'active' : 'inactive';
  }

  /** Expands the single status filter back into the two server-side flags. */
  #statusToQuery(status: string): Partial<CustomerQuery> {
    switch (status) {
      case 'active':
        return { isActive: true, isBlocked: false };
      case 'inactive':
        return { isActive: false };
      case 'blocked':
        return { isBlocked: true };
      default:
        return {};
    }
  }

  #patchQuery(params: Record<string, string | number | null>): void {
    void this.#router.navigate([], {
      relativeTo: this.#route,
      queryParams: params,
      queryParamsHandling: 'merge',
    });
  }
}
