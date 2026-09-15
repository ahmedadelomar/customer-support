import { Component, type OnChanges, computed, inject, input, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { CollaborationService } from '../../data-access/collaboration.service';
import type { Watcher } from '../../data-access/interfaces/collaboration.interface';

/**
 * Watchers section for the ticket properties panel (Agent Dashboard / Team collaboration).
 * Self-contained: fetches its own list from `ticketId`, rather than threading watcher state through
 * `PropertiesPanelComponent`, which already owns enough of its own inline-edit state.
 */
@Component({
  selector: 'app-watchers-panel',
  imports: [TranslatePipe],
  templateUrl: './watchers-panel.component.html',
})
export class WatchersPanelComponent implements OnChanges {
  readonly #service = inject(CollaborationService);
  readonly #language = inject(LanguageService);

  readonly ticketId = input.required<string>();

  readonly watchers = signal<Watcher[]>([]);
  readonly loading = signal(true);
  readonly toggling = signal(false);

  readonly isWatching = computed(() => this.watchers().some((w) => w.isCurrentUser));

  ngOnChanges(): void {
    this.#load();
  }

  name(watcher: Watcher): string {
    return this.#language.pick({ en: watcher.nameEn, ar: watcher.nameAr });
  }

  toggle(): void {
    this.toggling.set(true);
    const request$ = this.isWatching()
      ? this.#service.unwatch(this.ticketId())
      : this.#service.watch(this.ticketId());

    request$.subscribe({
      next: () => {
        this.toggling.set(false);
        this.#load();
      },
      error: () => this.toggling.set(false),
    });
  }

  #load(): void {
    this.loading.set(true);
    this.#service.watchers(this.ticketId()).subscribe({
      next: (watchers) => {
        this.watchers.set(watchers);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
