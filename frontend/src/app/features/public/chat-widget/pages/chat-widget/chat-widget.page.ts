import { DOCUMENT } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { WidgetChatHubService } from '../../data-access/widget-chat-hub.service';
import { WidgetChatService } from '../../data-access/widget-chat.service';
import { ChatAuthorType, type StoredChatSession, type WidgetChatMessage, type WidgetChatSession } from '../../data-access/interfaces/widget-chat.interface';

const OFFLINE_FALLBACK_SECONDS = 60;

function storageKey(channelAccountId: string): string {
  return `chat-widget:session:${channelAccountId}`;
}

function readStoredSession(channelAccountId: string): StoredChatSession | null {
  try {
    const raw = localStorage.getItem(storageKey(channelAccountId));
    return raw ? (JSON.parse(raw) as StoredChatSession) : null;
  } catch {
    return null; // Private browsing, or a blocked store — the widget still works, just without resume.
  }
}

function writeStoredSession(session: StoredChatSession): void {
  try {
    localStorage.setItem(storageKey(session.channelAccountId), JSON.stringify(session));
  } catch {
    // Same as above — losing the ability to resume after a reload is an acceptable degradation.
  }
}

function clearStoredSession(channelAccountId: string): void {
  try {
    localStorage.removeItem(storageKey(channelAccountId));
  } catch {
    // Nothing to do if storage is unavailable.
  }
}

/**
 * The chat panel shown inside the loader script's iframe (Communication Channels / Live chat,
 * CS-303). Deliberately its own top-level route, not part of the agent or admin shells — it is
 * meant to be loaded cross-origin, on whatever page a customer is browsing, so it carries no
 * assumption of being logged in and no shared layout chrome.
 */
@Component({
  selector: 'app-chat-widget',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './chat-widget.page.html',
})
export class ChatWidgetPage {
  readonly #route = inject(ActivatedRoute);
  readonly #chat = inject(WidgetChatService);
  readonly #hub = inject(WidgetChatHubService);
  readonly #translate = inject(TranslateService);
  readonly #document = inject(DOCUMENT);

  readonly ChatAuthorType = ChatAuthorType;

  readonly channelAccountId = this.#route.snapshot.queryParamMap.get('account') ?? '';

  readonly starting = signal(false);
  readonly session = signal<WidgetChatSession | null>(null);
  readonly token = signal<string>('');
  readonly messages = signal<WidgetChatMessage[]>([]);
  readonly draft = signal('');
  readonly remoteTyping = signal(false);
  readonly waitingSeconds = signal(0);
  readonly showOfflineForm = signal(false);
  readonly offlineName = signal('');
  readonly offlineEmail = signal('');
  readonly ratingSubmitted = signal(false);

  readonly isEnded = () => this.session()?.status === 'Ended' || this.session()?.status === 'Abandoned';
  readonly isWaiting = () => this.session()?.status === 'Waiting';

  #waitingTimer: ReturnType<typeof setInterval> | null = null;
  #typingTimeout: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    const lang = this.#route.snapshot.queryParamMap.get('lang') === 'ar' ? 'ar' : 'en';
    // Direct TranslateService use, not LanguageService — the widget shares this browser origin's
    // storage with the real agent app, and must never overwrite the agent's own language preference.
    void this.#translate.use(lang);
    this.#document.documentElement.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
    this.#document.documentElement.setAttribute('lang', lang);

    const stored = this.channelAccountId ? readStoredSession(this.channelAccountId) : null;
    if (stored) {
      this.#resume(stored);
    }

    effect(() => {
      const message = this.#hub.messageReceived();
      if (message) {
        this.messages.update((list) => [...list, message]);
        this.#hub.markRead();
      }
    });

    effect(() => {
      const updated = this.#hub.sessionEvent();
      if (updated) {
        this.session.set(updated);
        if (updated.status === 'Ended' || updated.status === 'Abandoned') {
          this.#stopWaitingTimer();
          if (this.channelAccountId) clearStoredSession(this.channelAccountId);
        }
      }
    });

    effect(() => {
      const typing = this.#hub.typingChanged();
      if (typing !== null) this.remoteTyping.set(typing);
    });

    // Reconnected after a drop — the socket may have missed messages while it was down, so
    // re-fetch the transcript rather than trust whatever arrived (or didn't) over the gap.
    effect(() => {
      if (this.#hub.connected() && this.session()) {
        this.#refetchMessages();
      }
    });
  }

  send(): void {
    const body = this.draft().trim();
    if (!body) return;

    if (!this.session()) {
      this.#start(body);
      return;
    }

    this.draft.set('');
    this.#hub.setTyping(false);
    void this.#hub.sendMessage(body);
  }

  onDraftInput(value: string): void {
    this.draft.set(value);
    if (!this.session()) return;

    this.#hub.setTyping(true);
    if (this.#typingTimeout) clearTimeout(this.#typingTimeout);
    this.#typingTimeout = setTimeout(() => this.#hub.setTyping(false), 3000);
  }

  submitOfflineForm(): void {
    const sessionId = this.session()?.id;
    if (!sessionId) return;

    this.#chat.identify(sessionId, this.token(), this.offlineName() || null, this.offlineEmail() || null).subscribe({
      next: () => {
        this.#chat.end(sessionId, this.token()).subscribe((updated) => {
          this.session.set(updated);
          this.showOfflineForm.set(false);
          this.#stopWaitingTimer();
          if (this.channelAccountId) clearStoredSession(this.channelAccountId);
        });
      },
    });
  }

  rate(value: number): void {
    const sessionId = this.session()?.id;
    if (!sessionId || this.ratingSubmitted()) return;

    this.#chat.rate(sessionId, this.token(), value).subscribe(() => this.ratingSubmitted.set(true));
  }

  authorLabel(message: WidgetChatMessage): string {
    if (message.authorType === ChatAuthorType.Agent) return message.authorDisplayName ?? 'Agent';
    if (message.authorType === ChatAuthorType.Bot) return 'Bot';
    return this.#translate.instant('chat.widget.you');
  }

  close(): void {
    if (window.parent !== window) {
      window.parent.postMessage({ type: 'chat-widget:close' }, '*');
    }
  }

  #start(initialMessage: string): void {
    if (!this.channelAccountId || this.starting()) return;

    this.starting.set(true);
    this.#chat.start({
      channelAccountId: this.channelAccountId,
      visitorKey: null,
      visitorName: null,
      pageUrl: window.parent !== window ? document.referrer || null : window.location.href,
      language: this.#translate.currentLang || 'en',
      initialMessage,
    }).subscribe({
      next: (result) => {
        this.starting.set(false);
        this.draft.set('');
        this.session.set(result.session);
        this.token.set(result.token);
        writeStoredSession({
          channelAccountId: this.channelAccountId, sessionId: result.sessionId,
          visitorKey: result.visitorKey, token: result.token,
        });
        this.messages.set([]);
        void this.#hub.connect(result.sessionId, result.token);
        this.#startWaitingTimer(result.session.startedAt);
      },
      error: () => this.starting.set(false),
    });
  }

  #resume(stored: StoredChatSession): void {
    this.token.set(stored.token);
    this.#chat.messages(stored.sessionId, stored.token).subscribe({
      next: (messages) => {
        this.messages.set(messages);
        this.session.set({
          id: stored.sessionId, visitorKey: stored.visitorKey, visitorName: null, visitorEmail: null,
          customerId: null, ticketId: null, assignedAgentId: null, assignedAgentName: null,
          status: 'Active', startedAt: new Date().toISOString(), endedAt: null,
        });
        void this.#hub.connect(stored.sessionId, stored.token);
      },
      error: () => clearStoredSession(stored.channelAccountId),
    });
  }

  #refetchMessages(): void {
    const sessionId = this.session()?.id;
    if (!sessionId) return;
    this.#chat.messages(sessionId, this.token()).subscribe((messages) => this.messages.set(messages));
  }

  #startWaitingTimer(startedAt: string): void {
    this.#stopWaitingTimer();
    const start = new Date(startedAt).getTime();
    this.#waitingTimer = setInterval(() => {
      if (!this.isWaiting()) {
        this.#stopWaitingTimer();
        return;
      }
      const seconds = Math.floor((Date.now() - start) / 1000);
      this.waitingSeconds.set(seconds);
      if (seconds >= OFFLINE_FALLBACK_SECONDS) {
        this.showOfflineForm.set(true);
      }
    }, 1000);
  }

  #stopWaitingTimer(): void {
    if (this.#waitingTimer) {
      clearInterval(this.#waitingTimer);
      this.#waitingTimer = null;
    }
  }
}
