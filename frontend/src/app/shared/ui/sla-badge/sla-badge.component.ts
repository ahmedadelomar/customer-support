import { Component, computed, inject, input } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

/** Two hours out is treated as "approaching" when no clock-specific threshold is available to the caller. */
const DEFAULT_WARNING_WINDOW_MS = 2 * 60 * 60 * 1000;

export type SlaBadgeState = 'ok' | 'warning' | 'breached' | 'none';

/**
 * Shared SLA indicator (SLA and Automation / Response and resolution targets) — the ticket list,
 * ticket detail and dashboard all show the same remaining-time badge rather than three ad-hoc
 * renderings of the same clock. Rose when breached (shows the overdue amount), amber inside the
 * warning window, neutral otherwise, and a plain "no policy" state when the ticket has no clock.
 */
@Component({
  selector: 'app-sla-badge',
  template: `
    <span
      class="inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium"
      [class]="classes()"
    >
      @if (state() === 'breached') {
        <span aria-hidden="true">⚠</span>
      }
      {{ label() }}
    </span>
  `,
})
export class SlaBadgeComponent {
  readonly #translate = inject(TranslateService);

  /** ISO due time for this commitment, or null when the ticket has no matching SLA policy. */
  readonly dueAt = input<string | null>(null);
  readonly isBreached = input(false);
  /** Working minutes paused so far — shown so an agent can see why the clock isn't moving. */
  readonly pausedMinutes = input<number | null>(null);
  readonly warningWindowMs = input(DEFAULT_WARNING_WINDOW_MS);

  readonly state = computed<SlaBadgeState>(() => {
    if (!this.dueAt()) return 'none';
    if (this.isBreached()) return 'breached';

    const remaining = new Date(this.dueAt()!).getTime() - Date.now();
    return remaining <= this.warningWindowMs() ? 'warning' : 'ok';
  });

  readonly classes = computed(() => {
    switch (this.state()) {
      case 'breached':
        return 'border-rose-200 bg-rose-50 text-rose-700';
      case 'warning':
        return 'border-amber-200 bg-amber-50 text-amber-700';
      case 'ok':
        return 'border-emerald-200 bg-emerald-50 text-emerald-700';
      default:
        return 'border-slate-200 bg-slate-50 text-slate-500';
    }
  });

  readonly label = computed(() => {
    const due = this.dueAt();
    if (!due) {
      return this.#translate.instant('sla.badge.none');
    }

    const diffMs = new Date(due).getTime() - Date.now();
    const duration = formatDurationParts(Math.abs(diffMs));

    if (this.isBreached() || diffMs < 0) {
      return this.#translate.instant('sla.badge.overdue', duration);
    }

    const paused = this.pausedMinutes();
    if (paused && paused > 0) {
      return this.#translate.instant('sla.badge.paused', formatDurationParts(paused * 60_000));
    }

    return this.#translate.instant('sla.badge.left', duration);
  });
}

/** `{ hours, minutes }`, both plain numbers so the translation string controls the unit labels per locale. */
function formatDurationParts(ms: number): { hours: number; minutes: number } {
  const totalMinutes = Math.max(0, Math.round(ms / 60_000));
  return { hours: Math.floor(totalMinutes / 60), minutes: totalMinutes % 60 };
}
