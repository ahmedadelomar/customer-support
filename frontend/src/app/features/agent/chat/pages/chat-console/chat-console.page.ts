import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import type { TicketCategoryAdmin } from '../../../../admin/categories/data-access/interfaces/ticket-category.interface';
import { TicketCategoriesService } from '../../../../admin/categories/data-access/ticket-categories.service';
import { ChatHubService } from '../../data-access/chat-hub.service';
import { ChatService } from '../../data-access/chat.service';
import { ChatAuthorType, type ChatMessage, type ChatSession } from '../../data-access/interfaces/chat.interface';

const SOUND_PREF_KEY = 'chat.soundEnabled';
const NOTIFY_PREF_KEY = 'chat.notifyEnabled';
const TYPING_IDLE_MS = 3000;

function readBoolPref(key: string, fallback: boolean): boolean {
  try {
    const raw = localStorage.getItem(key);
    return raw === null ? fallback : raw === 'true';
  } catch {
    return fallback;
  }
}

function writeBoolPref(key: string, value: boolean): void {
  try {
    localStorage.setItem(key, String(value));
  } catch {
    // Private browsing or a blocked store — the toggle still works for this tab, just not remembered.
  }
}

/**
 * Agent chat console (Communication Channels / Live chat, CS-303). Two panes: the queue (waiting +
 * this agent's own active chats) on the left, the open conversation on the right. Drafts are kept
 * per session id so switching between concurrent chats never loses an unsent reply.
 */
@Component({
  selector: 'app-chat-console',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './chat-console.page.html',
})
export class ChatConsolePage {
  readonly #chat = inject(ChatService);
  readonly #hub = inject(ChatHubService);
  readonly #categories = inject(TicketCategoriesService);
  readonly #toast = inject(ToastService);
  readonly #translate = inject(TranslateService);

  readonly ChatAuthorType = ChatAuthorType;

  readonly waiting = signal<ChatSession[]>([]);
  readonly active = signal<ChatSession[]>([]);
  readonly loading = signal(true);

  readonly selectedSessionId = signal<string | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly messagesLoading = signal(false);
  readonly remoteTyping = signal(false);

  readonly drafts = signal<Map<string, string>>(new Map());
  readonly categories = signal<TicketCategoryAdmin[]>([]);
  readonly promoting = signal(false);
  readonly promoteCategoryId = signal('');

  readonly soundEnabled = signal(readBoolPref(SOUND_PREF_KEY, true));
  readonly notifyEnabled = signal(readBoolPref(NOTIFY_PREF_KEY, true));

  #typingTimeout: ReturnType<typeof setTimeout> | null = null;

  readonly selectedSession = () =>
    this.active().find((s) => s.id === this.selectedSessionId())
    ?? this.waiting().find((s) => s.id === this.selectedSessionId())
    ?? null;

  readonly draftText = () => this.drafts().get(this.selectedSessionId() ?? '') ?? '';

  constructor() {
    this.#categories.list().subscribe((categories) => this.categories.set(categories));
    if (this.notifyEnabled() && 'Notification' in window && Notification.permission === 'default') {
      void Notification.requestPermission();
    }

    this.loadQueue();

    effect(() => {
      const message = this.#hub.messageReceived();
      if (!message) return;
      if (message.chatSessionId === this.selectedSessionId()) {
        this.messages.update((list) => [...list, message]);
        void this.#hub.markRead(message.chatSessionId);
      }
      this.#bumpSessionActivity(message.chatSessionId);
    });

    effect(() => {
      const evt = this.#hub.sessionEvent();
      if (!evt) return;
      this.#applySessionEvent(evt.event, evt.session);
    });

    effect(() => {
      const typing = this.#hub.typingChanged();
      if (!typing) return;
      if (typing.sessionId === this.selectedSessionId()) {
        this.remoteTyping.set(typing.isTyping);
      }
    });

    effect(() => {
      const read = this.#hub.messagesRead();
      if (!read || read.sessionId !== this.selectedSessionId()) return;
      const ids = new Set(read.messageIds);
      this.messages.update((list) =>
        list.map((m) => (ids.has(m.id) ? { ...m, readAt: new Date().toISOString() } : m)),
      );
    });
  }

  loadQueue(): void {
    this.loading.set(true);
    this.#chat.queue().subscribe({
      next: (queue) => {
        this.waiting.set(queue.waiting);
        this.active.set(queue.active);
        this.loading.set(false);

        const teamIds = [...new Set(queue.waiting.map((s) => s.queuedForTeamId).filter((id): id is string => !!id))];
        void this.#hub.joinTeamQueues(teamIds);
      },
      error: () => this.loading.set(false),
    });
  }

  select(session: ChatSession): void {
    this.selectedSessionId.set(session.id);
    this.remoteTyping.set(false);
    this.messagesLoading.set(true);
    void this.#hub.joinSession(session.id);

    this.#chat.messages(session.id).subscribe({
      next: (messages) => {
        this.messages.set(messages);
        this.messagesLoading.set(false);
        void this.#hub.markRead(session.id);
      },
      error: () => this.messagesLoading.set(false),
    });
  }

  accept(session: ChatSession, event: Event): void {
    event.stopPropagation();
    this.#chat.accept(session.id).subscribe({
      next: (updated) => {
        this.waiting.update((list) => list.filter((s) => s.id !== session.id));
        this.active.update((list) => [...list, updated]);
        this.select(updated);
      },
      error: () => this.loadQueue(),
    });
  }

  onDraftInput(value: string): void {
    const sessionId = this.selectedSessionId();
    if (!sessionId) return;

    this.drafts.update((map) => new Map(map).set(sessionId, value));
    this.#hub.setTyping(sessionId, true);

    if (this.#typingTimeout) clearTimeout(this.#typingTimeout);
    this.#typingTimeout = setTimeout(() => this.#hub.setTyping(sessionId, false), TYPING_IDLE_MS);
  }

  async send(): Promise<void> {
    const sessionId = this.selectedSessionId();
    const body = this.draftText().trim();
    if (!sessionId || !body) return;

    this.drafts.update((map) => new Map(map).set(sessionId, ''));
    this.#hub.setTyping(sessionId, false);
    await this.#hub.sendMessage(sessionId, body);
  }

  endChat(): void {
    const sessionId = this.selectedSessionId();
    if (!sessionId) return;
    if (!confirm(this.#translate.instant('chat.confirmEnd'))) return;

    this.#chat.end(sessionId).subscribe({
      next: (updated) => this.#applySessionEvent('session.ended', updated),
    });
  }

  openPromote(): void {
    this.promoteCategoryId.set(this.categories()[0]?.id ?? '');
    this.promoting.set(true);
  }

  confirmPromote(): void {
    const sessionId = this.selectedSessionId();
    const categoryId = this.promoteCategoryId();
    if (!sessionId || !categoryId) return;

    this.#chat.promote(sessionId, { categoryId }).subscribe({
      next: () => {
        this.promoting.set(false);
        this.#toast.success('chat.promoted');
        this.loadQueue();
      },
      error: () => this.promoting.set(false),
    });
  }

  toggleSound(): void {
    this.soundEnabled.update((v) => !v);
    writeBoolPref(SOUND_PREF_KEY, this.soundEnabled());
  }

  toggleNotify(): void {
    this.notifyEnabled.update((v) => !v);
    writeBoolPref(NOTIFY_PREF_KEY, this.notifyEnabled());
    if (this.notifyEnabled() && 'Notification' in window && Notification.permission === 'default') {
      void Notification.requestPermission();
    }
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.#translate.currentLang || 'en');
  }

  authorLabel(message: ChatMessage): string {
    if (message.authorType === ChatAuthorType.Agent) return message.authorDisplayName ?? 'Agent';
    if (message.authorType === ChatAuthorType.Bot) return 'Bot';
    if (message.authorType === ChatAuthorType.System) return 'System';
    return message.authorDisplayName ?? 'Visitor';
  }

  #applySessionEvent(event: string, session: ChatSession): void {
    if (event === 'session.queued') {
      if (!this.waiting().some((s) => s.id === session.id)) {
        this.waiting.update((list) => [...list, session]);
        this.#announceNewChat();
      }
      return;
    }

    if (event === 'session.accepted') {
      this.waiting.update((list) => list.filter((s) => s.id !== session.id));
      this.active.update((list) => (list.some((s) => s.id === session.id) ? list : [...list, session]));
      return;
    }

    if (event === 'session.ended' || event === 'session.promoted' || event === 'session.abandoned') {
      this.waiting.update((list) => list.filter((s) => s.id !== session.id));
      this.active.update((list) => list.filter((s) => s.id !== session.id));
      if (this.selectedSessionId() === session.id) {
        this.selectedSessionId.set(null);
        this.messages.set([]);
      }
      return;
    }

    // session.updated (identification) — refresh whichever list currently holds it.
    this.waiting.update((list) => list.map((s) => (s.id === session.id ? session : s)));
    this.active.update((list) => list.map((s) => (s.id === session.id ? session : s)));
  }

  #bumpSessionActivity(sessionId: string): void {
    this.active.update((list) =>
      list.map((s) => (s.id === sessionId && s.id !== this.selectedSessionId() ? { ...s, unreadCount: s.unreadCount + 1 } : s)),
    );
  }

  #announceNewChat(): void {
    if (this.soundEnabled()) {
      try {
        const audio = new Audio('data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQAAAAA=');
        void audio.play();
      } catch {
        // Autoplay can be blocked before the first user gesture — silent is an acceptable fallback.
      }
    }

    if (this.notifyEnabled() && 'Notification' in window && Notification.permission === 'granted') {
      new Notification(this.#translate.instant('chat.newChatNotificationTitle'));
    }
  }
}
