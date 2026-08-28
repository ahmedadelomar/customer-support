import { Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Page navigation for list screens. Emits intent only; the parent owns the query state so the
 * page number stays in the URL and survives a refresh.
 */
@Component({
  selector: 'app-pagination',
  imports: [TranslatePipe],
  template: `
    @if (totalPages() > 1) {
      <nav
        class="flex flex-wrap items-center justify-between gap-3 py-3"
        [attr.aria-label]="'common.pagination' | translate"
      >
        <p class="text-sm text-slate-500">
          {{ 'common.showingRange' | translate: { from: firstItem(), to: lastItem(), total: totalCount() } }}
        </p>

        <div class="flex items-center gap-1">
          <button
            type="button"
            class="btn-secondary px-3 py-1.5"
            [disabled]="page() <= 1"
            (click)="pageChange.emit(page() - 1)"
          >
            {{ 'common.previous' | translate }}
          </button>

          @for (item of pageWindow(); track item) {
            @if (item === -1) {
              <span class="px-2 text-slate-400">…</span>
            } @else {
              <button
                type="button"
                class="min-w-9 rounded-lg px-3 py-1.5 text-sm"
                [class]="item === page() ? 'bg-brand-700 text-white' : 'text-slate-700 hover:bg-slate-100'"
                [attr.aria-current]="item === page() ? 'page' : null"
                (click)="pageChange.emit(item)"
              >
                {{ item }}
              </button>
            }
          }

          <button
            type="button"
            class="btn-secondary px-3 py-1.5"
            [disabled]="page() >= totalPages()"
            (click)="pageChange.emit(page() + 1)"
          >
            {{ 'common.next' | translate }}
          </button>
        </div>
      </nav>
    }
  `,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();

  readonly pageChange = output<number>();

  readonly totalPages = computed(() =>
    this.pageSize() <= 0 ? 0 : Math.ceil(this.totalCount() / this.pageSize()),
  );

  readonly firstItem = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );

  readonly lastItem = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  /**
   * Page numbers to render. `-1` marks an ellipsis. Always shows the first and last page plus a
   * window around the current one, so the control stays a fixed width at any page count.
   */
  readonly pageWindow = computed<number[]>(() => {
    const total = this.totalPages();
    const current = this.page();

    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const pages = new Set<number>([1, total, current]);
    if (current - 1 > 1) pages.add(current - 1);
    if (current + 1 < total) pages.add(current + 1);

    const sorted = [...pages].sort((a, b) => a - b);
    const result: number[] = [];

    sorted.forEach((value, index) => {
      if (index > 0 && value - sorted[index - 1] > 1) {
        result.push(-1);
      }
      result.push(value);
    });

    return result;
  });
}
