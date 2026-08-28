import { NgClass } from '@angular/common';
import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** KPI tile used across dashboards and list headers. */
@Component({
  selector: 'app-state-card',
  imports: [NgClass, TranslatePipe],
  template: `
    <div class="card flex items-center gap-4 p-4">
      <div
        class="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-lg"
        [ngClass]="badgeClasses()"
        aria-hidden="true"
      >
        {{ icon() }}
      </div>

      <div class="min-w-0">
        <p class="truncate text-sm text-slate-500">{{ labelKey() | translate }}</p>

        @if (loading()) {
          <div class="mt-1 h-6 w-16 animate-pulse rounded bg-slate-100"></div>
        } @else {
          <p class="text-xl font-semibold" [ngClass]="valueClasses()">{{ value() }}</p>
        }

        @if (hintKey()) {
          <p class="mt-0.5 text-xs text-slate-400">{{ hintKey()! | translate }}</p>
        }
      </div>
    </div>
  `,
})
export class StateCardComponent {
  readonly labelKey = input.required<string>();
  readonly value = input<string | number>('—');
  readonly icon = input('•');
  readonly hintKey = input<string | null>(null);
  readonly loading = input(false);

  /** Tailwind classes for the icon badge, e.g. `bg-brand-50 text-brand-700`. */
  readonly badgeClasses = input('bg-brand-50 text-brand-700');
  readonly valueClasses = input('text-slate-900');
}
