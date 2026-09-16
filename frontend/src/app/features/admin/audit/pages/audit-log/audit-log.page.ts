import { Component, OnInit, type TemplateRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditAction } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { DataTableComponent } from '../../../../../shared/ui/data-table/data-table.component';
import type { DataTableColumn } from '../../../../../shared/ui/data-table/data-table.models';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { PaginationComponent } from '../../../../../shared/ui/pagination/pagination.component';
import { SearchInputComponent } from '../../../../../shared/ui/search-input/search-input.component';
import { AuditDetailPanelComponent } from '../../ui/audit-detail-panel/audit-detail-panel.component';
import { AuditService, type AuditLogListItem, type AuditQuery } from '../../data-access/audit.service';

/** Chip colour per action, so a delete or a failed sign-in is visible while scanning. */
const ACTION_CLASSES: Record<number, string> = {
  [AuditAction.Create]: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  [AuditAction.Update]: 'border-amber-200 bg-amber-50 text-amber-700',
  [AuditAction.Delete]: 'border-rose-200 bg-rose-50 text-rose-700',
  [AuditAction.Read]: 'border-slate-200 bg-slate-50 text-slate-600',
  [AuditAction.Login]: 'border-slate-200 bg-slate-50 text-slate-600',
  [AuditAction.LoginFailed]: 'border-red-300 bg-red-50 text-red-700',
  [AuditAction.Logout]: 'border-slate-200 bg-slate-50 text-slate-600',
  [AuditAction.PermissionChanged]: 'border-violet-200 bg-violet-50 text-violet-700',
  [AuditAction.Export]: 'border-blue-200 bg-blue-50 text-blue-700',
  [AuditAction.ConfigChanged]: 'border-indigo-200 bg-indigo-50 text-indigo-700',
};

/**
 * Audit trail viewer (Security &amp; Administration / Audit logs).
 *
 * The date range defaults to the last 7 days, matching the server: an unfiltered scan of this table
 * is the one query that can take the database down.
 */
@Component({
  selector: 'app-audit-log',
  imports: [
    FormsModule,
    TranslatePipe,
    DataTableComponent,
    PageHeaderComponent,
    PaginationComponent,
    SearchInputComponent,
    AuditDetailPanelComponent,
  ],
  templateUrl: './audit-log.page.html',
})
export class AuditLogPage implements OnInit {
  readonly #service = inject(AuditService);
  readonly #language = inject(LanguageService);

  readonly rows = signal<AuditLogListItem[]>([]);
  readonly loading = signal(true);
  readonly exporting = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(50);
  readonly totalCount = signal(0);

  readonly searchTerm = signal('');
  readonly fromFilter = signal(this.#defaultFrom());
  readonly toFilter = signal('');
  readonly entityTypeFilter = signal('');
  readonly actionFilter = signal('');

  readonly entityTypes = signal<string[]>([]);
  readonly selectedId = signal<string | null>(null);

  readonly locale = computed(() => this.#language.locale());

  // `viewChild` cannot be declared on a native `#private` field.
  private readonly actionCell = viewChild<TemplateRef<{ $implicit: AuditLogListItem }>>('actionCell');

  readonly actionOptions = [
    { value: AuditAction.Create, labelKey: 'enums.auditAction.0' },
    { value: AuditAction.Update, labelKey: 'enums.auditAction.1' },
    { value: AuditAction.Delete, labelKey: 'enums.auditAction.2' },
    { value: AuditAction.Login, labelKey: 'enums.auditAction.4' },
    { value: AuditAction.LoginFailed, labelKey: 'enums.auditAction.5' },
    { value: AuditAction.Logout, labelKey: 'enums.auditAction.6' },
    { value: AuditAction.PermissionChanged, labelKey: 'enums.auditAction.7' },
    { value: AuditAction.Export, labelKey: 'enums.auditAction.8' },
    { value: AuditAction.ConfigChanged, labelKey: 'enums.auditAction.9' },
  ];

  readonly columns = computed<DataTableColumn<AuditLogListItem>[]>(() => [
    { id: 'occurredAt', headerKey: 'admin.audit.columns.timestamp', type: 'datetime', field: 'occurredAt' },
    {
      id: 'userName',
      headerKey: 'admin.audit.columns.actor',
      type: 'text',
      value: (row) => row.userName ?? '—',
    },
    { id: 'action', headerKey: 'admin.audit.columns.action', type: 'custom', template: this.actionCell() },
    { id: 'entityType', headerKey: 'admin.audit.columns.entityType', type: 'text', field: 'entityType' },
    {
      id: 'entityId',
      headerKey: 'admin.audit.columns.entityId',
      type: 'text',
      hideOnMobile: true,
      value: (row) => row.entityId ?? '—',
    },
    {
      id: 'ipAddress',
      headerKey: 'admin.audit.columns.ip',
      type: 'text',
      hideOnMobile: true,
      value: (row) => row.ipAddress ?? '—',
    },
  ]);

  actionClasses(action: AuditAction): string {
    return ACTION_CLASSES[action] ?? 'border-slate-200 bg-slate-50 text-slate-600';
  }

  ngOnInit(): void {
    this.#service.entityTypes().subscribe({ next: (types) => this.entityTypes.set(types) });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list(this.#query()).subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
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

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  openDetail(row: AuditLogListItem): void {
    this.selectedId.set(row.id);
  }

  closeDetail(): void {
    this.selectedId.set(null);
  }

  export(): void {
    this.exporting.set(true);

    this.#service.export(this.#query()).subscribe({
      next: (blob) => {
        this.exporting.set(false);

        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.exporting.set(false),
    });
  }

  #query(): AuditQuery {
    return {
      search: this.searchTerm() || undefined,
      from: this.fromFilter() ? new Date(this.fromFilter()).toISOString() : undefined,
      to: this.toFilter() ? new Date(this.toFilter()).toISOString() : undefined,
      entityType: this.entityTypeFilter() || undefined,
      action: this.actionFilter() === '' ? undefined : (Number(this.actionFilter()) as AuditAction),
      page: this.page(),
      pageSize: this.pageSize(),
    };
  }

  /** Seven days back, as an `input[type=date]` value. Mirrors the server's own default window. */
  #defaultFrom(): string {
    const date = new Date();
    date.setDate(date.getDate() - 7);
    return date.toISOString().slice(0, 10);
  }
}
