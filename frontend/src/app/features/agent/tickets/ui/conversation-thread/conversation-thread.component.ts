import { Component, ElementRef, type OnChanges, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MessageDirection, TicketEventType } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import { ticketEventIcon, ticketEventSentence } from '../../../../../shared/utils/ticket-event-sentence';
import { QuickRepliesService } from '../../../quick-replies/data-access/quick-replies.service';
import type { QuickReply, QuickReplyRenderResult } from '../../../quick-replies/data-access/interfaces/quick-reply.interface';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketEvent } from '../../data-access/interfaces/ticket-event.interface';
import type { TicketMessage } from '../../data-access/interfaces/ticket-message.interface';
import { QuickReplyPickerComponent, type QuickReplyInsertResult } from '../quick-reply-picker/quick-reply-picker.component';

type ComposerMode = 'reply' | 'note';

/** Matches only at a word boundary, so a URL path mid-sentence is never expanded. */
const SHORTCUT_PATTERN = /(?:^|\s)(\/[a-z0-9_-]+)\s$/i;

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
  imports: [FormsModule, TranslatePipe, EmptyStateComponent, QuickReplyPickerComponent],
  templateUrl: './conversation-thread.component.html',
})
export class ConversationThreadComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #quickReplies = inject(QuickRepliesService);
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

  readonly pickerOpen = signal(false);
  readonly pendingQuickReplyId = signal<string | null>(null);
  readonly unresolvedTokens = signal<string[]>([]);

  readonly locale = computed(() => this.#language.locale());

  // `viewChild` cannot be declared on a native `#private` field.
  private readonly composerInput = viewChild<ElementRef<HTMLTextAreaElement>>('composerInput');

  /** Shortcut -> quick reply, for instant local matching as the agent types. Refetched per ticket. */
  #shortcutIndex = new Map<string, QuickReply>();
  /** Rendered body per quick-reply id, scoped to THIS ticket — repeated use of the same shortcut is instant. */
  #renderCache = new Map<string, QuickReplyRenderResult>();
  #expandingShortcut = false;

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
    this.pendingQuickReplyId.set(null);
    this.unresolvedTokens.set([]);
    this.#renderCache.clear();
    this.load();
    this.#loadShortcutIndex();
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

  #loadShortcutIndex(): void {
    this.#shortcutIndex.clear();
    this.#quickReplies.list().subscribe({
      next: (items) => {
        for (const item of items) {
          if (item.shortcut) this.#shortcutIndex.set(item.shortcut.toLowerCase(), item);
        }
      },
    });
  }

  // --- Quick replies ------------------------------------------------------------------------
  openPicker(): void {
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  onQuickReplyInserted(result: QuickReplyInsertResult): void {
    this.pickerOpen.set(false);
    this.#insertAtCursor(result.body);
    this.pendingQuickReplyId.set(result.quickReplyId);
    this.unresolvedTokens.set(result.unresolvedTokens);
  }

  /**
   * Watches for `/word` followed by a trailing space — matched only at a word boundary, so a URL
   * path mid-sentence is never expanded. Renders (or reuses the per-ticket cache) and replaces the
   * shortcut text in place.
   */
  onBodyInput(): void {
    if (this.#expandingShortcut) return;

    const textarea = this.composerInput()?.nativeElement;
    if (!textarea) return;

    const cursor = textarea.selectionStart ?? this.body().length;
    const textBeforeCursor = this.body().slice(0, cursor);
    const match = SHORTCUT_PATTERN.exec(textBeforeCursor);
    if (!match) return;

    const shortcut = match[1].toLowerCase();
    const quickReply = this.#shortcutIndex.get(shortcut);
    if (!quickReply) return;

    // `match[0]` includes the leading word-boundary character (start-of-string or a space) and the
    // trailing space; the shortcut itself (`match[1]`) sits somewhere inside it — locate it exactly
    // rather than assuming a fixed offset, so both boundary cases (start-of-string vs. after a space)
    // are handled the same way.
    const shortcutOffsetInMatch = match[0].indexOf(match[1]);
    const matchStart = cursor - match[0].length + shortcutOffsetInMatch;
    const matchEnd = cursor;

    const cached = this.#renderCache.get(quickReply.id);
    if (cached) {
      this.#replaceRange(matchStart, matchEnd, cached.body);
      this.unresolvedTokens.set(cached.unresolvedTokens);
      this.pendingQuickReplyId.set(quickReply.id);
      return;
    }

    this.#expandingShortcut = true;
    this.#quickReplies.render(quickReply.id, this.ticketId()).subscribe({
      next: (result) => {
        this.#expandingShortcut = false;
        this.#renderCache.set(quickReply.id, result);
        this.#replaceRange(matchStart, matchEnd, result.body);
        this.unresolvedTokens.set(result.unresolvedTokens);
        this.pendingQuickReplyId.set(quickReply.id);
      },
      error: () => {
        this.#expandingShortcut = false;
      },
    });
  }

  #insertAtCursor(text: string): void {
    const textarea = this.composerInput()?.nativeElement;
    const value = this.body();

    if (!textarea) {
      this.body.set(value + text);
      return;
    }

    const start = textarea.selectionStart ?? value.length;
    const end = textarea.selectionEnd ?? value.length;
    this.#replaceRange(start, end, text);
  }

  #replaceRange(start: number, end: number, text: string): void {
    const value = this.body();
    const next = value.slice(0, start) + text + value.slice(end);
    this.body.set(next);

    const textarea = this.composerInput()?.nativeElement;
    if (textarea) {
      const caret = start + text.length;
      queueMicrotask(() => {
        textarea.focus();
        textarea.setSelectionRange(caret, caret);
      });
    }
  }

  send(): void {
    const text = this.body().trim();
    if (!text || this.sending()) {
      return;
    }

    this.sending.set(true);

    const request$ =
      this.mode() === 'reply'
        ? this.#service.reply(this.ticketId(), { bodyText: text, quickReplyId: this.pendingQuickReplyId() ?? undefined })
        : this.#service.addInternalNote(this.ticketId(), { bodyText: text });

    request$.subscribe({
      next: () => {
        this.sending.set(false);
        this.body.set('');
        this.pendingQuickReplyId.set(null);
        this.unresolvedTokens.set([]);
        this.#toast.success(this.mode() === 'reply' ? 'tickets.replied' : 'tickets.noteAdded');
        this.load();
        this.sent.emit();
      },
      error: () => this.sending.set(false),
    });
  }
}
