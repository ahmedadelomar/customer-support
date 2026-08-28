import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from './core/services/toast.service';

/**
 * Root shell. Renders the routed view plus the global toast stack, which lives here so it
 * survives navigation between the agent, portal and admin areas.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslatePipe],
  template: `
    <router-outlet />

    <div
      class="pointer-events-none fixed bottom-4 end-4 z-50 flex w-80 flex-col gap-2"
      role="status"
      aria-live="polite"
    >
      @for (toast of toasts(); track toast.id) {
        <div
          class="pointer-events-auto flex items-start gap-3 rounded-lg border p-3 shadow-lg"
          [class]="toastClasses(toast.severity)"
        >
          <p class="flex-1 text-sm">{{ toast.message | translate }}</p>
          <button
            type="button"
            class="text-lg leading-none opacity-60 hover:opacity-100"
            [attr.aria-label]="'common.dismiss' | translate"
            (click)="dismiss(toast.id)"
          >
            &times;
          </button>
        </div>
      }
    </div>
  `,
})
export class App {
  readonly #toast = inject(ToastService);

  readonly toasts = this.#toast.toasts;

  dismiss(id: number): void {
    this.#toast.dismiss(id);
  }

  toastClasses(severity: string): string {
    switch (severity) {
      case 'success':
        return 'border-emerald-200 bg-emerald-50 text-emerald-800';
      case 'error':
        return 'border-rose-200 bg-rose-50 text-rose-800';
      case 'warning':
        return 'border-amber-200 bg-amber-50 text-amber-800';
      default:
        return 'border-slate-200 bg-white text-slate-800';
    }
  }
}
