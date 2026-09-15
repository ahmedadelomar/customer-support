import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { MessageDirection } from '../../../../../../../core/models/enums';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../../../../shared/utils/relative-time';
import { TicketsService } from '../../../../data-access/tickets.service';
import type { TicketMessage } from '../../../../data-access/interfaces/ticket-message.interface';

type ComposerMode = 'reply' | 'note';

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

  readonly ticketId = input.required<string>();
  readonly canReply = input(false);
  readonly canAddInternalNote = input(false);
  readonly isTerminal = input(false);

  /** Lets the parent refresh header fields (status may leave New on a reply). */
  readonly sent = output<void>();

  readonly MessageDirection = MessageDirection;

  readonly messages = signal<TicketMessage[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly pageSize = 50;
  readonly totalCount = signal(0);

  readonly mode = signal<ComposerMode>('reply');
  readonly body = signal('');
  readonly sending = signal(false);

  readonly locale = computed(() => this.#language.locale());

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
  }

  setMode(mode: ComposerMode): void {
    this.mode.set(mode);
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
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
