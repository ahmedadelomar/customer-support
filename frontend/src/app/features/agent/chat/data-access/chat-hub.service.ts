import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from '../../../../core/auth/auth.service';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { ChatMessage, ChatSession } from './interfaces/chat.interface';

export interface ChatSessionEvent {
  event: 'session.queued' | 'session.accepted' | 'session.ended' | 'session.promoted' | 'session.abandoned' | 'session.updated';
  session: ChatSession;
}

/** The five event names <c>ChatHub</c> broadcasts to a session or team-queue group; one `.on` per name, all folded into the same signal. */
const SESSION_EVENT_NAMES: ChatSessionEvent['event'][] = [
  'session.queued', 'session.accepted', 'session.ended', 'session.promoted', 'session.abandoned', 'session.updated',
];

/**
 * Agent-side client for `/hubs/chat` (Communication Channels / Live chat, CS-303). A single shared
 * connection, like `CollaborationHubService` — the console needs both the team-queue groups and
 * whichever session it currently has open on the same socket.
 */
@Injectable({ providedIn: 'root' })
export class ChatHubService {
  readonly #auth = inject(AuthService);
  readonly #config = inject(AppConfigService);

  readonly messageReceived = signal<ChatMessage | null>(null);
  readonly sessionEvent = signal<ChatSessionEvent | null>(null);
  readonly typingChanged = signal<{ sessionId: string; isTyping: boolean } | null>(null);
  readonly messagesRead = signal<{ sessionId: string; messageIds: string[] } | null>(null);

  #connection: HubConnection | null = null;
  #connecting: Promise<void> | null = null;
  readonly #joinedTeamIds = new Set<string>();
  #joinedSessionId: string | null = null;

  async joinTeamQueues(teamIds: readonly string[]): Promise<void> {
    await this.#ensureConnected();
    for (const teamId of teamIds) {
      if (this.#joinedTeamIds.has(teamId)) continue;
      await this.#invoke('JoinTeamQueue', teamId);
      this.#joinedTeamIds.add(teamId);
    }
  }

  async joinSession(sessionId: string): Promise<void> {
    await this.#ensureConnected();
    if (this.#joinedSessionId && this.#joinedSessionId !== sessionId) {
      await this.#invoke('LeaveSession', this.#joinedSessionId);
    }
    this.#joinedSessionId = sessionId;
    await this.#invoke('JoinSession', sessionId);
  }

  async leaveSession(sessionId: string): Promise<void> {
    if (this.#joinedSessionId !== sessionId) return;
    this.#joinedSessionId = null;
    await this.#invoke('LeaveSession', sessionId);
  }

  sendMessage(sessionId: string, body: string): Promise<void> {
    return this.#invoke('SendMessage', sessionId, body);
  }

  setTyping(sessionId: string, isTyping: boolean): void {
    void this.#invoke('Typing', sessionId, isTyping);
  }

  markRead(sessionId: string): void {
    void this.#invoke('MarkRead', sessionId);
  }

  async #invoke(method: string, ...args: unknown[]): Promise<void> {
    try {
      if (this.#connection?.state === HubConnectionState.Connected) {
        await this.#connection.invoke(method, ...args);
      }
    } catch {
      // Best-effort, same as the collaboration hub — a dropped invoke is not surfaced as an error.
    }
  }

  async #ensureConnected(): Promise<void> {
    if (!this.#connection) {
      this.#connection = new HubConnectionBuilder()
        .withUrl(`${this.#config.apiUrl}/hubs/chat`, { accessTokenFactory: () => this.#auth.accessToken ?? '' })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.#connection.on('messageReceived', (message: ChatMessage) => this.messageReceived.set(message));
      this.#connection.on('typing', (payload: { sessionId: string; isTyping: boolean }) => this.typingChanged.set(payload));
      this.#connection.on('messagesRead', (payload: { sessionId: string; messageIds: string[] }) => this.messagesRead.set(payload));

      for (const eventName of SESSION_EVENT_NAMES) {
        this.#connection.on(eventName, (session: ChatSession) => this.sessionEvent.set({ event: eventName, session }));
      }

      this.#connection.onreconnected(() => {
        const teamIds = [...this.#joinedTeamIds];
        this.#joinedTeamIds.clear();
        void this.joinTeamQueues(teamIds);

        const sessionId = this.#joinedSessionId;
        if (sessionId) void this.#invoke('JoinSession', sessionId);
      });
    }

    if (this.#connection.state === HubConnectionState.Disconnected) {
      this.#connecting ??= this.#connection.start().finally(() => {
        this.#connecting = null;
      });
      await this.#connecting;
    }
  }
}
