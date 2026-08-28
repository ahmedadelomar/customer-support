import { DatePipe, DecimalPipe, NgClass, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../core/services/language.service';
import type {
  DataTableAction,
  DataTableColumn,
  SortState,
  StatusChipConfigMap,
} from './data-table.models';

/**
 * The single list surface used by every feature.
 *
 * Two renderings of the same data: a table on desktop and stacked cards below `md`, which is how
 * the Platform requirement for a mobile-friendly UI is met without a separate mobile route.
 * Sorting and paging are emitted, never handled internally, because the server owns both.
 */
@Component({
  selector: 'app-data-table',
  imports: [NgClass, NgTemplateOutlet, DatePipe, DecimalPipe, TranslatePipe],
  templateUrl: './data-table.component.html',
})
export class DataTableComponent<TRow extends { id: string }> {
  readonly #language = inject(LanguageService);

  readonly columns = input.required<DataTableColumn<TRow>[]>();
  readonly rows = input.required<TRow[]>();
  readonly loading = input(false);

  /** Chip styling per status code, supplied by the feature that owns the statuses. */
  readonly statusConfig = input<StatusChipConfigMap>({});

  readonly sort = input<SortState | null>(null);
  readonly emptyMessageKey = input('common.noResults');

  /** Number of skeleton rows drawn while loading, matched to the requested page size. */
  readonly skeletonRows = input(5);

  readonly sortChange = output<SortState>();
  readonly rowClick = output<TRow>();

  /** Id of the row whose action menu is open; only one may be open at a time. */
  readonly openMenuRowId = signal<string | null>(null);

  readonly locale = computed(() => this.#language.locale());

  readonly desktopColumns = computed(() => this.columns());
  readonly mobileColumns = computed(() =>
    this.columns().filter((c) => c.type !== 'actions' && !c.hideOnMobile),
  );
  readonly actionsColumn = computed(() => this.columns().find((c) => c.type === 'actions'));

  readonly skeletonRange = computed(() =>
    Array.from({ length: this.skeletonRows() }, (_, i) => i),
  );

  onHeaderClick(column: DataTableColumn<TRow>): void {
    if (!column.sortable) {
      return;
    }

    const current = this.sort();
    const sortDescending = current?.sortBy === column.id ? !current.sortDescending : false;
    this.sortChange.emit({ sortBy: column.id, sortDescending });
  }

  sortIcon(column: DataTableColumn<TRow>): '' | '▲' | '▼' {
    const current = this.sort();
    if (!column.sortable || current?.sortBy !== column.id) {
      return '';
    }

    return current.sortDescending ? '▼' : '▲';
  }

  /** Resolves the display value for a cell, honouring `value`, `localized` and plain fields. */
  cellValue(column: DataTableColumn<TRow>, row: TRow): string {
    if (column.value) {
      return column.value(row);
    }

    const raw = this.rawValue(column, row);
    if (raw === null || raw === undefined || raw === '') {
      return '—';
    }

    if (column.type === 'localized' && typeof raw === 'object') {
      return this.#language.pick(raw as { en: string; ar: string });
    }

    return String(raw);
  }

  rawValue(column: DataTableColumn<TRow>, row: TRow): unknown {
    const field = (column.field ?? column.id) as keyof TRow;
    return row[field];
  }

  numericValue(column: DataTableColumn<TRow>, row: TRow): number | null {
    const raw = this.rawValue(column, row);
    return typeof raw === 'number' ? raw : null;
  }

  dateValue(column: DataTableColumn<TRow>, row: TRow): string | null {
    const raw = this.rawValue(column, row);
    return typeof raw === 'string' ? raw : null;
  }

  booleanValue(column: DataTableColumn<TRow>, row: TRow): boolean {
    return Boolean(this.rawValue(column, row));
  }

  /** Chip config for the row status, falling back to neutral styling for unmapped codes. */
  statusFor(column: DataTableColumn<TRow>, row: TRow) {
    const code = String(this.rawValue(column, row) ?? '');
    return (
      this.statusConfig()[code] ?? {
        labelKey: code || 'common.unknown',
        chipClasses: 'bg-slate-100 text-slate-700 border-slate-200',
        dotClasses: 'bg-slate-400',
      }
    );
  }

  visibleActions(row: TRow): DataTableAction<TRow>[] {
    return (this.actionsColumn()?.actions ?? []).filter((a) => !a.visible || a.visible(row));
  }

  toggleMenu(row: TRow, event: Event): void {
    event.stopPropagation();
    this.openMenuRowId.update((current) => (current === row.id ? null : row.id));
  }

  runAction(action: DataTableAction<TRow>, row: TRow, event: Event): void {
    event.stopPropagation();
    this.openMenuRowId.set(null);
    action.run(row);
  }
}
