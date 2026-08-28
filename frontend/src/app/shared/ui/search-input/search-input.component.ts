import { Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Debounced search box. Debouncing lives here so no list page has to repeat it, and so a fast
 * typist does not fire one request per keystroke.
 */
@Component({
  selector: 'app-search-input',
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="relative">
      <input
        type="search"
        class="form-control ps-9"
        [placeholder]="placeholderKey() | translate"
        [ngModel]="draft()"
        (ngModelChange)="onInput($event)"
        [attr.aria-label]="placeholderKey() | translate"
      />
      <span class="pointer-events-none absolute inset-y-0 start-3 flex items-center text-slate-400">
        ⌕
      </span>
    </div>
  `,
})
export class SearchInputComponent {
  readonly value = input('');
  readonly placeholderKey = input('common.search');
  readonly debounceMs = input(350);

  readonly search = output<string>();

  readonly draft = signal('');

  #timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Keep the box in sync when the parent resets the term, e.g. on clearing all filters.
    effect(() => this.draft.set(this.value()));
  }

  onInput(next: string): void {
    this.draft.set(next);

    if (this.#timer) {
      clearTimeout(this.#timer);
    }

    this.#timer = setTimeout(() => this.search.emit(next.trim()), this.debounceMs());
  }
}
