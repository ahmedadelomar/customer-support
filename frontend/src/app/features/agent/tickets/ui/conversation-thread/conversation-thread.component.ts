import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MessageDirection, TicketEventType } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import { ticketEventIcon, ticketEventSentence } from '../../../../../shared/utils/ticket-event-sentence';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketEvent } from '../../data-access/interfaces/ticket-event.interface';
import type { TicketMessage } from '../../data-access/interfaces/ticket-message.interface';

type ComposerMode = 'reply' | 'note';

/** Event types already fully represented by a message bubble — shown inline would just duplicate it. */
const INLINE_EXCLUDED_EVENTS = new Set([
  TicketEventType.Created,
  TicketEventType.MessageAdded,
  TicketEventType.InternalNoteAdded,
]);

type TimelineRow =
  | { kind: 'message'; sortAt: number; message: TicketMessage }
  | { kind: 'event'; sortAt: number; event: TicketEvent };

/**
 * Centre column: the conversation thread, oldest first, with the reply/internal-note composer.
 * A visibly different composer per mode is what stops an internal note being sent to a customer
 * by accident — see the story's product rules.
 */
@Component({
  selector: 'app-conversation-thread',
  imports: [FormsModule, TranslatePipe, EmptyStateComponent],
  templateUrl: './conversation-thread.component.html',
})
export class ConversationThreadComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #translate = inject(TranslateService);

  readonly ticketId = input.required<string>();
  readonly canReply = input(false);
  readonly canAddInternalNote = input(false);
  readonly isTerminal = input(false);

  /** Lets the parent refresh header fields (status may leave New on a reply). */
  readonly sent = output<void>();

  readonly MessageDirection = MessageDirection;

  readonly messages = signal<TicketMessage[]>([]);
  readonly events = signal<TicketEvent[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly pageSize = 50;
  readonly totalCount = signal(0);

  readonly mode = signal<ComposerMode>('reply');
  readonly body = signal('');
  readonly sending = signal(false);

  readonly locale = computed(() => this.#language.locale());

  /**
   * Messages plus the non-redundant events that fall within the loaded messages' time window,
   * merged into one chronological list — slim single-line entries for events, full bubbles for
   * messages, per the story's "reads as a narrative" product rule. Bounding events to the loaded
   * window (rather than showing every fetched event) avoids an orphaned event appearing ahead of a
   * message page that has not been loaded yet.
   */
  readonly timeline = computed<TimelineRow[]>(() => {
    const msgs = this.messages();
    const rows: TimelineRow[] = msgs.map((message) => ({
      kind: 'message',
      sortAt: new Date(message.sentAt).getTime(),
      message,
    }));

    if (msgs.length > 0) {
      const start = new Date(msgs[0].sentAt).getTime();
      const end = new Date(msgs[msgs.length - 1].sentAt).getTime();

      for (const event of this.events()) {
        if (INLINE_EXCLUDED_EVENTS.has(event.eventType)) continue;
        const at = new Date(event.occurredAt).getTime();
        if (at < start || at > end) continue;
        rows.push({ kind: 'event', sortAt: at, event });
      }
    }

    rows.sort((a, b) => a.sortAt - b.sortAt);
    return rows;
  });

  ngOnChanges(): void {
    this.page.set(1);
    this.body.set('');
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.messages(this.ticketId(), this.page(), this.pageSize).subscribe({
      next: (result) => {
        this.messages.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.#loadEvents();
  }

  setMode(mode: ComposerMode): void {
    this.mode.set(mode);
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  eventIcon(event: TicketEvent): string {
    return ticketEventIcon(event.eventType);
  }

  eventSentenceKey(event: TicketEvent): string {
    return ticketEventSentence(event, this.#translate.instant('tickets.history.system')).key;
  }

  eventSentenceParams(event: TicketEvent): Record<string, string | number> {
    return ticketEventSentence(event, this.#translate.instant('tickets.history.system')).params;
  }

  #loadEvents(): void {
    this.#service.history(this.ticketId(), { includeSystem: false, pageSize: 200 }).subscribe({
      next: (page) => this.events.set(page.items),
      error: () => this.events.set([]),
    });
  }

  send(): void {
    const text = this.body().trim();
    if (!text || this.sending()) {
      return;
    }

    this.sending.set(true);

    const request$ =
      this.mode() === 'reply'
        ? this.#service.reply(this.ticketId(), { bodyText: text })
        : this.#service.addInternalNote(this.ticketId(), { bodyText: text });

    request$.subscribe({
      next: () => {
        this.sending.set(false);
        this.body.set('');
        this.#toast.success(this.mode() === 'reply' ? 'tickets.replied' : 'tickets.noteAdded');
        this.load();
        this.sent.emit();
      },
      error: () => this.sending.set(false),
    });
  }
}
