import { Component, OnInit, TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../../../core/auth/auth.service';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PERMISSIONS } from '../../../../../core/permissions';
import { AppConfigService } from '../../../../../core/services/app-config.service';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { DataTableComponent } from '../../../../../shared/ui/data-table/data-table.component';
import type { DataTableColumn, SortState } from '../../../../../shared/ui/data-table/data-table.models';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { PaginationComponent } from '../../../../../shared/ui/pagination/pagination.component';
import { SearchInputComponent } from '../../../../../shared/ui/search-input/search-input.component';
import { StateCardComponent } from '../../../../../shared/ui/state-card/state-card.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import { SavedTicketViewsService } from '../../data-access/saved-ticket-views.service';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketLookups } from '../../data-access/interfaces/ticket-lookups.interface';
import type { SavedTicketView } from '../../data-access/interfaces/saved-ticket-view.interface';
import type {
  TicketAssignmentFilter,
  TicketListItem,
  TicketQuery,
  TicketStatistics,
} from '../../data-access/interfaces/ticket.interface';
import { BulkAssignDialogComponent } from './ui/bulk-assign-dialog/bulk-assign-dialog.component';

/**
 * Ticket list (Ticket Management / Create and track tickets) — the screen agents spend their day
 * in. Mirrors `customer-list.page.ts`: URL-persisted filter state, computed translated columns.
 */
@Component({
  selector: 'app-ticket-list',
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
    BulkAssignDialogComponent,
  ],
  templateUrl: './ticket-list.page.html',
})
export class TicketListPage implements OnInit {
  readonly #service = inject(TicketsService);
  readonly #views = inject(SavedTicketViewsService);
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);
  readonly #toast = inject(ToastService);
  readonly #config = inject(AppConfigService);
  readonly #auth = inject(AuthService);

  readonly permissions = PERMISSIONS;
  readonly canBulkAssign = computed(() => this.#auth.hasPermission(PERMISSIONS.tickets.assign));

  readonly items = signal<TicketListItem[]>([]);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(this.#config.defaultPageSize);
  readonly totalCount = signal(0);
  readonly sort = signal<SortState | null>(null);

  readonly searchTerm = signal('');
  readonly assignment = signal<TicketAssignmentFilter>('mine');
  readonly statusId = signal<string>('');
  readonly priorityId = signal<string>('');
  readonly categoryId = signal<string>('');
  /** Not exposed as a visible control here — set only by an incoming link (e.g. the agent dashboard's tiles). */
  readonly slaState = signal<'breached' | 'duesoon' | 'ontrack' | ''>('');
  readonly dueToday = signal(false);

  readonly statistics = signal<TicketStatistics | null>(null);
  readonly lookups = signal<TicketLookups | null>(null);

  readonly savedViews = signal<SavedTicketView[]>([]);
  readonly savingView = signal(false);
  readonly newViewNameEn = signal('');
  readonly newViewNameAr = signal('');
  readonly showSaveViewForm = signal(false);

  readonly selectedTicketIds = signal<string[]>([]);
  readonly bulkAssignDialogOpen = signal(false);

  readonly assignmentTabs: { value: TicketAssignmentFilter; labelKey: string }[] = [
    { value: 'mine', labelKey: 'tickets.assignment.mine' },
    { value: 'team', labelKey: 'tickets.assignment.team' },
    { value: 'unassigned', labelKey: 'tickets.assignment.unassigned' },
    { value: 'all', labelKey: 'tickets.assignment.all' },
  ];

  readonly locale = computed(() => this.#language.locale());

  readonly statusOptions = computed(() => [
    { value: '', label: this.#translate.instant('tickets.filters.allStatuses') },
    ...(this.lookups()?.statuses ?? []).map((s) => ({ value: s.id, label: this.#language.pick({ en: s.nameEn, ar: s.nameAr }) })),
  ]);

  readonly priorityOptions = computed(() => [
    { value: '', label: this.#translate.instant('tickets.filters.allPriorities') },
    ...(this.lookups()?.priorities ?? []).map((p) => ({ value: p.id, label: this.#language.pick({ en: p.nameEn, ar: p.nameAr }) })),
  ]);

  readonly categoryOptions = computed(() => [
    { value: '', label: this.#translate.instant('tickets.filters.allCategories') },
    ...(this.lookups()?.categories ?? []).map((c) => ({
      value: c.id,
      label: `${'—'.repeat(c.depth)} ${this.#language.pick({ en: c.nameEn, ar: c.nameAr })}`,
    })),
  ]);

  // `viewChild` cannot be declared on a native `#private` field.
  private readonly slaTemplate = viewChild<TemplateRef<{ $implicit: TicketListItem }>>('slaTemplate');

  readonly columns = computed<DataTableColumn<TicketListItem>[]>(() => {
    const lang = this.#language.current();

    return [
      { id: 'number', headerKey: 'tickets.columns.number', type: 'text', sortable: true },
      {
        id: 'subject',
        headerKey: 'tickets.columns.subject',
        type: 'text',
        sortable: true,
        widthClass: 'max-w-xs',
      },
      {
        id: 'customer',
        headerKey: 'tickets.columns.customer',
        type: 'text',
        value: (row) =>
          lang === 'ar'
            ? row.customerDisplayNameAr || row.customerDisplayNameEn
            : row.customerDisplayNameEn || row.customerDisplayNameAr,
      },
      {
        id: 'category',
        headerKey: 'tickets.columns.category',
        type: 'text',
        hideOnMobile: true,
        value: (row) => (lang === 'ar' ? row.categoryNameAr : row.categoryNameEn),
      },
      {
        id: 'priority',
        headerKey: 'tickets.columns.priority',
        type: 'text',
        value: (row) => (lang === 'ar' ? row.priorityNameAr : row.priorityNameEn),
      },
      {
        id: 'status',
        headerKey: 'tickets.columns.status',
        type: 'text',
        value: (row) => (lang === 'ar' ? row.statusNameAr : row.statusNameEn),
      },
      {
        id: 'assignee',
        headerKey: 'tickets.columns.assignee',
        type: 'text',
        hideOnMobile: true,
        value: (row) => {
          if (!row.assignedAgentId) return this.#translate.instant('tickets.unassignedLabel');
          return lang === 'ar'
            ? row.assignedAgentNameAr || row.assignedAgentNameEn || ''
            : row.assignedAgentNameEn || row.assignedAgentNameAr || '';
        },
      },
      {
        id: 'sla',
        headerKey: 'tickets.columns.sla',
        type: 'custom',
        template: this.slaTemplate(),
        value: (row) => this.slaLabel(row),
      },
      {
        id: 'createdat',
        headerKey: 'tickets.columns.created',
        type: 'date',
        field: 'createdAt',
        sortable: true,
        hideOnMobile: true,
      },
    ];
  });

  ngOnInit(): void {
    this.#service.lookups().subscribe((lookups) => this.lookups.set(lookups));
    this.#loadSavedViews();

    this.#route.queryParamMap.subscribe((params) => {
      this.page.set(Number(params.get('page')) || 1);
      this.pageSize.set(Number(params.get('pageSize')) || this.#config.defaultPageSize);
      this.searchTerm.set(params.get('search') ?? '');
      this.assignment.set((params.get('assignment') as TicketAssignmentFilter) || 'mine');
      this.statusId.set(params.get('statusId') ?? '');
      this.priorityId.set(params.get('priorityId') ?? '');
      this.categoryId.set(params.get('categoryId') ?? '');
      this.slaState.set((params.get('slaState') as 'breached' | 'duesoon' | 'ontrack' | null) ?? '');
      this.dueToday.set(params.get('dueToday') === 'true');

      const sortBy = params.get('sortBy');
      this.sort.set(sortBy ? { sortBy, sortDescending: params.get('desc') === 'true' } : null);

      this.load();
    });
  }

  #query(): TicketQuery {
    return {
      page: this.page(),
      pageSize: this.pageSize(),
      search: this.searchTerm() || undefined,
      assignment: this.assignment(),
      statusId: this.statusId() || undefined,
      priorityId: this.priorityId() || undefined,
      categoryId: this.categoryId() || undefined,
      slaState: this.slaState() || undefined,
      dueToday: this.dueToday() || undefined,
      sortBy: this.sort()?.sortBy,
      sortDescending: this.sort()?.sortDescending,
    };
  }

  load(): void {
    this.loading.set(true);
    // A fresh page of rows invalidates any previous selection — the table's own view resets the
    // same way in response to its `rows()` input changing.
    this.selectedTicketIds.set([]);

    this.#service.list(this.#query()).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.#service.statistics(this.#query()).subscribe({
      next: (stats) => this.statistics.set(stats),
    });
  }

  onSearch(term: string): void {
    this.#patchQuery({ search: term || null, page: 1 });
  }

  selectAssignment(value: TicketAssignmentFilter): void {
    this.#patchQuery({ assignment: value, page: 1 });
  }

  onFilterChange(): void {
    this.#patchQuery({
      statusId: this.statusId() || null,
      priorityId: this.priorityId() || null,
      categoryId: this.categoryId() || null,
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
    this.#router.navigate([], { relativeTo: this.#route, queryParams: { assignment: 'mine' } });
  }

  openTicket(row: TicketListItem): void {
    void this.#router.navigate(['/agent/tickets', row.id]);
  }

  /** Relative due-date label; the caller styles it red/amber via `slaClasses`. */
  slaLabel(row: TicketListItem): string {
    if (row.isFirstResponseBreached || row.isResolutionBreached) {
      return this.#translate.instant('tickets.kpis.breached');
    }
    if (!row.resolutionDueAt) {
      return this.#translate.instant('tickets.noDueDate');
    }
    return relativeTime(row.resolutionDueAt, this.locale());
  }

  slaClasses(row: TicketListItem): string {
    if (row.isFirstResponseBreached || row.isResolutionBreached) {
      return 'text-rose-600 font-medium';
    }
    if (row.resolutionDueAt && new Date(row.resolutionDueAt).getTime() - Date.now() < 2 * 60 * 60 * 1000) {
      return 'text-amber-600 font-medium';
    }
    return 'text-slate-600';
  }

  // --- Saved views -----------------------------------------------------------------------
  #loadSavedViews(): void {
    this.#views.list().subscribe((views) => this.savedViews.set(views));
  }

  applyView(view: SavedTicketView): void {
    try {
      const filters = JSON.parse(view.filtersJson) as Record<string, string | number | null>;
      this.#router.navigate([], { relativeTo: this.#route, queryParams: { ...filters, page: 1 } });
    } catch {
      // A malformed filter set is simply ignored rather than crashing the list.
    }
  }

  toggleSaveViewForm(): void {
    this.showSaveViewForm.update((v) => !v);
  }

  saveCurrentView(): void {
    if (!this.newViewNameEn().trim() || !this.newViewNameAr().trim()) {
      return;
    }

    this.savingView.set(true);
    const filtersJson = JSON.stringify({
      assignment: this.assignment(),
      statusId: this.statusId() || undefined,
      priorityId: this.priorityId() || undefined,
      categoryId: this.categoryId() || undefined,
      search: this.searchTerm() || undefined,
    });

    this.#views.create({ nameEn: this.newViewNameEn(), nameAr: this.newViewNameAr(), filtersJson }).subscribe({
      next: () => {
        this.savingView.set(false);
        this.showSaveViewForm.set(false);
        this.newViewNameEn.set('');
        this.newViewNameAr.set('');
        this.#toast.success('common.save');
        this.#loadSavedViews();
      },
      error: () => this.savingView.set(false),
    });
  }

  deleteView(view: SavedTicketView, event: Event): void {
    event.stopPropagation();
    this.#views.delete(view.id).subscribe({ next: () => this.#loadSavedViews() });
  }

  viewLabel(view: SavedTicketView): string {
    return this.#language.pick({ en: view.nameEn, ar: view.nameAr });
  }

  // --- Bulk assign -------------------------------------------------------------------------
  onSelectionChange(ids: string[]): void {
    this.selectedTicketIds.set(ids);
  }

  openBulkAssign(): void {
    this.bulkAssignDialogOpen.set(true);
  }

  closeBulkAssign(): void {
    this.bulkAssignDialogOpen.set(false);
  }

  onBulkAssignCompleted(): void {
    this.load();
  }

  #patchQuery(params: Record<string, string | number | null>): void {
    void this.#router.navigate([], {
      relativeTo: this.#route,
      queryParams: params,
      queryParamsHandling: 'merge',
    });
  }
}
