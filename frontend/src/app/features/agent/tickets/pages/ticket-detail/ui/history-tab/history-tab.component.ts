import {
  Component,
  ElementRef,
  type OnChanges,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TicketEventType } from '../../../../../../../core/models/enums';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../../../../shared/utils/relative-time';
import { ticketEventIcon, ticketEventSentence } from '../../../../../../../shared/utils/ticket-event-sentence';
import { TicketsService } from '../../../../data-access/tickets.service';
import type { TicketEvent } from '../../../../data-access/interfaces/ticket-event.interface';

interface TimelineGroup {
  key: string;
  label: string;
  items: TicketEvent[];
}

const EVENT_TYPE_OPTIONS = [
  TicketEventType.Created,
  TicketEventType.StatusChanged,
  TicketEventType.PriorityChanged,
  TicketEventType.CategoryChanged,
  TicketEventType.Assigned,
  TicketEventType.Unassigned,
  TicketEventType.Escalated,
  TicketEventType.MessageAdded,
  TicketEventType.InternalNoteAdded,
  TicketEventType.AttachmentAdded,
  TicketEventType.SlaBreached,
  TicketEventType.Merged,
  TicketEventType.Reopened,
  TicketEventType.Resolved,
  TicketEventType.Closed,
  TicketEventType.DepartmentChanged,
  TicketEventType.TagsChanged,
  TicketEventType.WatcherAdded,
  TicketEventType.FollowUpCreated,
];

/**
 * The History tab (Ticket Management / Ticket history, CS-205): a read-only, keyset-paged timeline
 * of everything that happened to the ticket. Always fetches with `includeSystem: true` and filters
 * automation entries out client-side — this is what lets the "Show system events" toggle be instant
 * (no reload) and lets the hidden count be exact for whatever has already loaded. Event-type
 * filtering, by contrast, is sent to the server (see `reload()`), since it changes which rows the
 * keyset cursor should even be walking.
 */
@Component({
  selector: 'app-history-tab',
  imports: [FormsModule, TranslatePipe, EmptyStateComponent],
  templateUrl: './history-tab.component.html',
})
export class HistoryTabComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);

  readonly ticketId = input.required<string>();

  readonly eventTypeOptions = EVENT_TYPE_OPTIONS;
  readonly typeFilter = signal<TicketEventType[]>([]);
  readonly showSystem = signal(false);
  readonly filterOpen = signal(false);

  readonly entries = signal<TicketEvent[]>([]);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);

  readonly locale = computed(() => this.#language.locale());

  readonly hiddenSystemCount = computed(() =>
    this.showSystem() ? 0 : this.entries().filter((e) => e.isSystemGenerated).length,
  );

  readonly visibleEntries = computed(() =>
    this.showSystem() ? this.entries() : this.entries().filter((e) => !e.isSystemGenerated),
  );

  readonly groups = computed<TimelineGroup[]>(() => {
    const locale = this.locale();
    const dateFormatter = new Intl.DateTimeFormat(locale, { dateStyle: 'long' });

    const groups: TimelineGroup[] = [];
    const todayKey = this.#dateKey(new Date());
    const yesterdayKey = this.#dateKey(new Date(Date.now() - 86_400_000));

    for (const entry of this.visibleEntries()) {
      const key = this.#dateKey(new Date(entry.occurredAt));
      const last = groups[groups.length - 1];

      if (last?.key === key) {
        last.items.push(entry);
        continue;
      }

      const label =
        key === todayKey
          ? this.#translate.instant('tickets.history.today')
          : key === yesterdayKey
            ? this.#translate.instant('tickets.history.yesterday')
            : dateFormatter.format(new Date(entry.occurredAt));

      groups.push({ key, label, items: [entry] });
    }

    return groups;
  });

  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');

  constructor() {
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;
      if (!element) return;

      const observer = new IntersectionObserver((entries) => {
        if (entries[0]?.isIntersecting) {
          this.loadMore();
        }
      });

      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });
  }

  ngOnChanges(): void {
    this.reload();
  }

  reload(): void {
    this.entries.set([]);
    this.hasMore.set(false);
    this.loading.set(true);
    this.#fetch(undefined);
  }

  loadMore(): void {
    const last = this.entries().at(-1);
    if (!last || this.loadingMore() || !this.hasMore()) {
      return;
    }

    this.loadingMore.set(true);
    this.#fetch({ before: last.occurredAt, beforeId: last.id });
  }

  toggleFilterPanel(): void {
    this.filterOpen.update((open) => !open);
  }

  isTypeChecked(type: TicketEventType): boolean {
    return this.typeFilter().includes(type);
  }

  toggleType(type: TicketEventType, checked: boolean): void {
    this.typeFilter.update((current) => (checked ? [...current, type] : current.filter((t) => t !== type)));
    this.reload();
  }

  clearTypeFilter(): void {
    this.typeFilter.set([]);
    this.reload();
  }

  toggleShowSystem(): void {
    this.showSystem.update((v) => !v);
  }

  sentenceKey(event: TicketEvent): string {
    return ticketEventSentence(event, this.#translate.instant('tickets.history.system')).key;
  }

  sentenceParams(event: TicketEvent): Record<string, string | number> {
    return ticketEventSentence(event, this.#translate.instant('tickets.history.system')).params;
  }

  icon(event: TicketEvent): string {
    return ticketEventIcon(event.eventType);
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  #fetch(cursor: { before: string; beforeId: string } | undefined): void {
    this.#service
      .history(this.ticketId(), {
        ...cursor,
        eventTypes: this.typeFilter().length ? this.typeFilter() : undefined,
        includeSystem: true,
        pageSize: 50,
      })
      .subscribe({
        next: (page) => {
          this.entries.update((current) => [...current, ...page.items]);
          this.hasMore.set(page.hasMore);
          this.loading.set(false);
          this.loadingMore.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.loadingMore.set(false);
        },
      });
  }

  #dateKey(date: Date): string {
    return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
  }
}
