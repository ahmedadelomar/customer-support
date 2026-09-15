import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { QuickRepliesService } from '../../../quick-replies/data-access/quick-replies.service';
import type { QuickReply } from '../../../quick-replies/data-access/interfaces/quick-reply.interface';

export interface QuickReplyInsertResult {
  quickReplyId: string;
  body: string;
  unresolvedTokens: string[];
}

/**
 * Searchable, keyboard-navigable quick-reply picker (Agent Dashboard / Quick replies) — a speed
 * feature, so arrow keys + Enter must work without ever reaching for the mouse. Grouped by scope,
 * ordered by usage (the order the list endpoint already returns). Selecting a row renders it against
 * the current ticket and emits the resolved body for the composer to insert at the cursor.
 */
@Component({
  selector: 'app-quick-reply-picker',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './quick-reply-picker.component.html',
})
export class QuickReplyPickerComponent implements OnChanges {
  readonly #service = inject(QuickRepliesService);
  readonly #language = inject(LanguageService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();

  readonly inserted = output<QuickReplyInsertResult>();
  readonly closed = output<void>();

  readonly searchTerm = signal('');
  readonly items = signal<QuickReply[]>([]);
  readonly loading = signal(true);
  readonly activeIndex = signal(0);
  readonly rendering = signal(false);

  readonly lang = computed(() => this.#language.current());

  readonly filtered = computed<QuickReply[]>(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return this.items();

    return this.items().filter((item) => {
      const title = (this.lang() === 'ar' ? item.titleAr : item.titleEn).toLowerCase();
      const body = (this.lang() === 'ar' ? item.bodyAr : item.bodyEn).toLowerCase();
      return title.includes(term) || body.includes(term) || (item.shortcut ?? '').toLowerCase().includes(term);
    });
  });

  ngOnChanges(): void {
    if (!this.open()) return;

    this.searchTerm.set('');
    this.activeIndex.set(0);
    this.loading.set(true);

    this.#service.list().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  title(item: QuickReply): string {
    return this.lang() === 'ar' ? item.titleAr : item.titleEn;
  }

  bodyPreview(item: QuickReply): string {
    const body = this.lang() === 'ar' ? item.bodyAr : item.bodyEn;
    return body.length > 100 ? `${body.slice(0, 100)}…` : body;
  }

  onSearchChange(): void {
    this.activeIndex.set(0);
  }

  onKeydown(event: KeyboardEvent): void {
    const count = this.filtered().length;
    if (count === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeIndex.update((i) => (i + 1) % count);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex.update((i) => (i - 1 + count) % count);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const item = this.filtered()[this.activeIndex()];
      if (item) this.select(item);
    } else if (event.key === 'Escape') {
      this.close();
    }
  }

  select(item: QuickReply): void {
    if (this.rendering()) return;

    this.rendering.set(true);
    this.#service.render(item.id, this.ticketId()).subscribe({
      next: (result) => {
        this.rendering.set(false);
        this.inserted.emit({ quickReplyId: item.id, body: result.body, unresolvedTokens: result.unresolvedTokens });
      },
      error: () => this.rendering.set(false),
    });
  }

  close(): void {
    this.closed.emit();
  }
}
