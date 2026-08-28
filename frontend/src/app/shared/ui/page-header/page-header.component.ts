import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Consistent page title, optional subtitle and a projected action area. */
@Component({
  selector: 'app-page-header',
  imports: [TranslatePipe],
  template: `
    <header class="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 class="text-xl font-semibold text-slate-900 sm:text-2xl">
          {{ titleKey() | translate }}
        </h1>
        @if (subtitleKey()) {
          <p class="mt-1 text-sm text-slate-500">{{ subtitleKey()! | translate }}</p>
        }
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <ng-content />
      </div>
    </header>
  `,
})
export class PageHeaderComponent {
  readonly titleKey = input.required<string>();
  readonly subtitleKey = input<string | null>(null);
}
