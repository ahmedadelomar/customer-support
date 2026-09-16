import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { WidgetChatMessage, WidgetChatSession } from './interfaces/widget-chat.interface';

/**
 * Widget-side client for `/hubs/chat`. Deliberately separate from the agent's `ChatHubService`
 * (different injectable, own connection) since the two authenticate completely differently — a
 * visitor's scoped token here, a real agent login there — and a visitor tab and an agent console
 * are never the same browser context anyway.
 */
@Injectable({ providedIn: 'root' })
export class WidgetChatHubService {
  readonly #config = inject(AppConfigService);

  readonly messageReceived = signal<WidgetChatMessage | null>(null);
  readonly sessionEvent = signal<WidgetChatSession | null>(null);
  readonly typingChanged = signal<boolean | null>(null);
  readonly connected = signal(false);

  #connection: HubConnection | null = null;
  #connecting: Promise<void> | null = null;
  #sessionId: string | null = null;
  #token = '';

  async connect(sessionId: string, token: string): Promise<void> {
    this.#sessionId = sessionId;
    this.#token = token;

    if (!this.#connection) {
      this.#connection = new HubConnectionBuilder()
        .withUrl(`${this.#config.apiUrl}/hubs/chat`, { accessTokenFactory: () => this.#token })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.#connection.on('messageReceived', (message: WidgetChatMessage) => this.messageReceived.set(message));
      this.#connection.on('typing', (payload: { isTyping: boolean }) => this.typingChanged.set(payload.isTyping));

      for (const eventName of ['session.accepted', 'session.updated', 'session.ended', 'session.promoted', 'session.abandoned']) {
        this.#connection.on(eventName, (session: WidgetChatSession) => this.sessionEvent.set(session));
      }

      this.#connection.onreconnected(() => {
        this.connected.set(true);
        if (this.#sessionId) void this.#invoke('JoinSession', this.#sessionId);
      });
      this.#connection.onreconnecting(() => this.connected.set(false));
      this.#connection.onclose(() => this.connected.set(false));
    }

    if (this.#connection.state === HubConnectionState.Disconnected) {
      this.#connecting ??= this.#connection.start().finally(() => {
        this.#connecting = null;
      });
      await this.#connecting;
    }

    this.connected.set(true);
    await this.#invoke('JoinSession', sessionId);
  }

  sendMessage(body: string): Promise<void> {
    return this.#sessionId ? this.#invoke('SendMessage', this.#sessionId, body) : Promise.resolve();
  }

  setTyping(isTyping: boolean): void {
    if (this.#sessionId) void this.#invoke('Typing', this.#sessionId, isTyping);
  }

  markRead(): void {
    if (this.#sessionId) void this.#invoke('MarkRead', this.#sessionId);
  }

  async #invoke(method: string, ...args: unknown[]): Promise<void> {
    try {
      if (this.#connection?.state === HubConnectionState.Connected) {
        await this.#connection.invoke(method, ...args);
      }
    } catch {
      // Best-effort, mirroring the agent-side hub client.
    }
  }
}
