import { DatePipe } from '@angular/common';
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
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ChannelKey, MessageDirection } from '../../../../core/models/enums';
import { LanguageService } from '../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../shared/utils/relative-time';
import type { Interaction, InteractionQuery } from '../customer.models';
import { InteractionHistoryService } from './interaction-history.service';

/** One local calendar day's worth of entries, keyed by a sortable date string for @for's track. */
interface TimelineGroup {
  key: string;
  label: string;
  items: Interaction[];
}

const CHANNEL_ICONS: Record<ChannelKey, string> = {
  [ChannelKey.Email]: '✉',
  [ChannelKey.WhatsApp]: '💬',
  [ChannelKey.LiveChat]: '⌨',
  [ChannelKey.Sms]: '📱',
  [ChannelKey.WebForm]: '📝',
  [ChannelKey.Portal]: '🌐',
  [ChannelKey.Phone]: '☎',
  [ChannelKey.Api]: '⚙',
  [ChannelKey.Internal]: '🔒',
};

/**
 * The History tab: a read-only, reverse-chronological timeline across every channel — see
 * `.squad/plans/customer-management/12-story-interaction-history-CS-103-interaction-history.md`.
 *
 * Loaded lazily: this component is only constructed once the History tab is actually selected
 * (`customer-detail.page.html` puts it inside an `@case`, which Angular does not instantiate for
 * an inactive branch), so a profile view that never opens the tab never fetches it.
 */
@Component({
  selector: 'app-interaction-timeline',
  imports: [DatePipe, FormsModule, RouterLink, TranslatePipe, EmptyStateComponent],
  templateUrl: './interaction-timeline.component.html',
})
export class InteractionTimelineComponent implements OnChanges {
  readonly #service = inject(InteractionHistoryService);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);

  readonly customerId = input.required<string>();

  readonly ChannelKey = ChannelKey;
  readonly MessageDirection = MessageDirection;
  readonly channelIcon = CHANNEL_ICONS;

  readonly channelFilter = signal<ChannelKey | ''>('');
  readonly directionFilter = signal<MessageDirection | ''>('');
  readonly fromFilter = signal('');
  readonly toFilter = signal('');

  readonly entries = signal<Interaction[]>([]);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);

  readonly locale = computed(() => this.#language.locale());

  readonly channelOptions = [
    { value: '' as const, labelKey: 'customers.history.filters.allChannels' },
    { value: ChannelKey.Email, labelKey: 'enums.channel.0' },
    { value: ChannelKey.WhatsApp, labelKey: 'enums.channel.1' },
    { value: ChannelKey.LiveChat, labelKey: 'enums.channel.2' },
    { value: ChannelKey.Sms, labelKey: 'enums.channel.3' },
    { value: ChannelKey.WebForm, labelKey: 'enums.channel.4' },
    { value: ChannelKey.Portal, labelKey: 'enums.channel.5' },
    { value: ChannelKey.Phone, labelKey: 'enums.channel.6' },
  ];

  readonly directionOptions = [
    { value: '' as const, labelKey: 'customers.history.filters.allDirections' },
    { value: MessageDirection.Inbound, labelKey: 'customers.history.direction.inbound' },
    { value: MessageDirection.Outbound, labelKey: 'customers.history.direction.outbound' },
  ];

  /**
   * Groups the flat, already-ordered `entries` list into calendar-day buckets. Safe to do purely
   * client-side: keyset pagination guarantees strictly descending (occurredAt, id) order across
   * pages, so concatenating pages never interleaves a later page's rows into an earlier group.
   */
  readonly groups = computed<TimelineGroup[]>(() => {
    // Read unconditionally, even though only the non-today/yesterday branch below uses it for
    // formatting: `computed` only re-tracks a dependency it actually read on a given run, and if
    // every visible entry happened to be from today, `locale()` would never execute — so switching
    // language while the tab is open would leave stale labels until something else changed.
    const locale = this.locale();
    const dateFormatter = new Intl.DateTimeFormat(locale, { dateStyle: 'long' });

    const groups: TimelineGroup[] = [];
    const todayKey = this.#dateKey(new Date());
    const yesterdayKey = this.#dateKey(new Date(Date.now() - 86_400_000));

    for (const entry of this.entries()) {
      const key = this.#dateKey(new Date(entry.occurredAt));
      const last = groups[groups.length - 1];

      if (last?.key === key) {
        last.items.push(entry);
        continue;
      }

      const label =
        key === todayKey
          ? this.#translate.instant('customers.history.today')
          : key === yesterdayKey
            ? this.#translate.instant('customers.history.yesterday')
            : dateFormatter.format(new Date(entry.occurredAt));

      groups.push({ key, label, items: [entry] });
    }

    return groups;
  });

  // `viewChild` (like `input`/`output`) cannot be declared on a native `#private` field — the
  // compiler needs to extract it as class metadata. TypeScript `private` still keeps it internal.
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');

  constructor() {
    // Wires the observer up once the sentinel renders (only once hasMore() is true — see the
    // template) and disconnects it whenever the sentinel goes away or a new one replaces it,
    // including on component destroy: `effect` calls the last `onCleanup` automatically then.
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;
      if (!element) {
        return;
      }

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

  onFilterChange(): void {
    this.reload();
  }

  clearFilters(): void {
    this.channelFilter.set('');
    this.directionFilter.set('');
    this.fromFilter.set('');
    this.toFilter.set('');
    this.reload();
  }

  #fetch(cursor: { before: string; beforeId: string } | undefined): void {
    // Explicit `=== ''` checks, not `||` — ChannelKey.Email and MessageDirection.Inbound are both
    // 0, which `value || undefined` would treat as falsy and silently drop the filter.
    const channel = this.channelFilter();
    const direction = this.directionFilter();

    const query: InteractionQuery = {
      ...cursor,
      channel: channel === '' ? undefined : channel,
      direction: direction === '' ? undefined : direction,
      from: this.fromFilter() || undefined,
      to: this.toFilter() || undefined,
    };

    this.#service.list(this.customerId(), query).subscribe({
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

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  agentName(entry: Interaction): string | undefined {
    if (!entry.agentNameEn && !entry.agentNameAr) return undefined;
    return this.#language.pick({ en: entry.agentNameEn ?? '', ar: entry.agentNameAr ?? '' });
  }

  #dateKey(date: Date): string {
    // Local calendar day, not UTC — an agent groups "today" by their own clock, not the server's.
    return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
  }
}
