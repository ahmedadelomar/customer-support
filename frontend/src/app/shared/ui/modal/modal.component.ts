import { Component, ElementRef, HostListener, effect, input, output, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * A minimal dialog shell: overlay, centred panel, Escape-to-close, backdrop-click-to-close, and
 * focus moved into the panel on open. First reusable modal in the codebase — a story that needs a
 * confirmation step or a small form (contact add/edit, verification) should use this rather than
 * building its own overlay, so every dialog in the product looks and behaves the same way.
 */
@Component({
  selector: 'app-modal',
  imports: [TranslatePipe],
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4"
        (click)="onBackdropClick($event)"
      >
        <div
          #panel
          class="w-full max-w-md rounded-xl bg-white p-5 shadow-xl"
          role="dialog"
          [attr.aria-modal]="true"
          [attr.aria-labelledby]="titleKey() ? 'modal-title' : null"
          tabindex="-1"
          (click)="$event.stopPropagation()"
        >
          <div class="mb-4 flex items-start justify-between gap-3">
            @if (titleKey()) {
              <h2 id="modal-title" class="text-base font-semibold text-slate-900">
                {{ titleKey()! | translate }}
              </h2>
            }
            <button
              type="button"
              class="ms-auto shrink-0 rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
              [attr.aria-label]="'common.close' | translate"
              (click)="close()"
            >
              ✕
            </button>
          </div>

          <ng-content />
        </div>
      </div>
    }
  `,
})
export class ModalComponent {
  readonly open = input(false);
  readonly titleKey = input<string | null>(null);
  readonly closed = output<void>();

  // Angular's `viewChild` (like `input`/`output`) cannot be declared on a native `#private`
  // field — the compiler needs to extract it as class metadata. TypeScript `private` still keeps
  // it out of the public API.
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  constructor() {
    // Move focus into the panel whenever it opens, so a keyboard user lands somewhere sensible
    // rather than on whatever was focused behind it.
    effect(() => {
      if (this.open()) {
        queueMicrotask(() => this.panel()?.nativeElement.focus());
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close();
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }

  close(): void {
    this.closed.emit();
  }
}
