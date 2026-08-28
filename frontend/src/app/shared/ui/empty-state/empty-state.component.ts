import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/** Shown when a list or panel has nothing to display, with an optional projected call to action. */
@Component({
  selector: 'app-empty-state',
  imports: [TranslatePipe],
  template: `
    <div class="flex flex-col items-center justify-center gap-3 px-6 py-12 text-center">
      <span class="text-3xl opacity-40" aria-hidden="true">{{ icon() }}</span>
      <p class="text-sm font-medium text-slate-700">{{ titleKey() | translate }}</p>
      @if (descriptionKey()) {
        <p class="max-w-sm text-sm text-slate-500">{{ descriptionKey()! | translate }}</p>
      }
      <ng-content />
    </div>
  `,
})
export class EmptyStateComponent {
  readonly titleKey = input.required<string>();
  readonly descriptionKey = input<string | null>(null);
  readonly icon = input('◌');
}
