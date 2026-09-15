import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { LanguageService } from '../../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import { CollaborationService } from '../../data-access/collaboration.service';
import type { Mention } from '../../data-access/interfaces/collaboration.interface';

/** The caller's own @mentions inbox (Agent Dashboard / Team collaboration), unread first. */
@Component({
  selector: 'app-mentions-inbox',
  imports: [PageHeaderComponent, EmptyStateComponent],
  templateUrl: './mentions-inbox.page.html',
})
export class MentionsInboxPage {
  readonly #service = inject(CollaborationService);
  readonly #language = inject(LanguageService);
  readonly #router = inject(Router);

  readonly items = signal<Mention[]>([]);
  readonly loading = signal(true);

  readonly locale = computed(() => this.#language.locale());
  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.mentions().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  mentionedByName(mention: Mention): string {
    return this.#language.pick({ en: mention.mentionedByNameEn, ar: mention.mentionedByNameAr });
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  /** Marks the mention read (if not already) and navigates to its ticket. */
  open(mention: Mention): void {
    if (!mention.readAt) {
      this.#service.markMentionRead(mention.id).subscribe();
      this.items.update((current) =>
        current.map((m) => (m.id === mention.id ? { ...m, readAt: new Date().toISOString() } : m)),
      );
    }

    void this.#router.navigate(['/agent/tickets', mention.ticketId]);
  }
}
