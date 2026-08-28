import type { TemplateRef } from '@angular/core';

/** Built-in cell renderers. `custom` defers to a caller-supplied template. */
export type DataTableColumnType =
  | 'text'
  | 'localized'
  | 'date'
  | 'datetime'
  | 'number'
  | 'boolean'
  | 'status'
  | 'chip'
  | 'custom'
  | 'actions';

export interface DataTableColumn<TRow = Record<string, unknown>> {
  /** Stable identifier, also used as the sort key sent to the API. */
  id: string;

  /** Translation key for the header. Empty renders no header text (used by the actions column). */
  headerKey: string;

  type: DataTableColumnType;

  /** Property to read from the row. Defaults to `id` when omitted. */
  field?: keyof TRow & string;

  sortable?: boolean;
  widthClass?: string;
  alignEnd?: boolean;

  /** Hidden on small screens; the mobile card view renders these instead. */
  hideOnMobile?: boolean;

  /** Template for `type: 'custom'`, receiving `{ $implicit: row }`. */
  template?: TemplateRef<{ $implicit: TRow }>;

  /** Row menu entries for `type: 'actions'`. */
  actions?: DataTableAction<TRow>[];

  /** Overrides the rendered value entirely; use for computed or formatted cells. */
  value?: (row: TRow) => string;
}

export interface DataTableAction<TRow = Record<string, unknown>> {
  /** Translation key for the menu item label. */
  labelKey: string;
  icon?: string;

  /** Conditional visibility per row — this is how status-driven actions are enforced. */
  visible?: (row: TRow) => boolean;
  disabled?: (row: TRow) => boolean;
  danger?: boolean;

  run: (row: TRow) => void;
}

/** Chip appearance for one status value, keyed by the status code. */
export interface StatusChipConfig {
  labelKey: string;
  chipClasses: string;
  dotClasses: string;
}

export type StatusChipConfigMap = Record<string, StatusChipConfig>;

export interface SortState {
  sortBy: string;
  sortDescending: boolean;
}
