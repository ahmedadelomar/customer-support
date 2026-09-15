import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from '../../../../core/auth/auth.service';
import { AppConfigService } from '../../../../core/services/app-config.service';
import type { PresenceEntry } from './interfaces/collaboration.interface';

/**
 * Client for the collaboration hub (`/hubs/collaboration`) — live presence and composing signals for
 * one ticket at a time. A single connection is shared app-wide (`providedIn: 'root'`) since a hub
 * connection is a real socket, not a per-component resource; `ticket-detail.page` owns joining and
 * leaving as the agent opens and closes tickets, and both the presence bar and the composer read the
 * same `presence` signal without needing it threaded through `@Input`s.
 */
@Injectable({ providedIn: 'root' })
export class CollaborationHubService {
  readonly #auth = inject(AuthService);
  readonly #config = inject(AppConfigService);

  readonly presence = signal<PresenceEntry[]>([]);

  #connection: HubConnection | null = null;
  #currentTicketId: string | null = null;
  #connecting: Promise<void> | null = null;

  async joinTicket(ticketId: string): Promise<void> {
    await this.#ensureConnected();

    if (this.#currentTicketId && this.#currentTicketId !== ticketId) {
      await this.#invoke('LeaveTicket', this.#currentTicketId);
    }

    this.#currentTicketId = ticketId;
    this.presence.set([]);
    await this.#invoke('JoinTicket', ticketId);
  }

  async leaveTicket(ticketId: string): Promise<void> {
    if (this.#currentTicketId !== ticketId) {
      return;
    }

    this.#currentTicketId = null;
    this.presence.set([]);
    await this.#invoke('LeaveTicket', ticketId);
  }

  startComposing(ticketId: string): void {
    void this.#invoke('StartComposing', ticketId);
  }

  stopComposing(ticketId: string): void {
    void this.#invoke('StopComposing', ticketId);
  }

  async #invoke(method: string, ...args: unknown[]): Promise<void> {
    try {
      if (this.#connection?.state === HubConnectionState.Connected) {
        await this.#connection.invoke(method, ...args);
      }
    } catch {
      // Presence is advisory — a dropped invoke (e.g. mid-reconnect) is never surfaced to the agent.
    }
  }

  async #ensureConnected(): Promise<void> {
    if (!this.#connection) {
      this.#connection = new HubConnectionBuilder()
        .withUrl(`${this.#config.apiUrl}/hubs/collaboration`, {
          accessTokenFactory: () => this.#auth.accessToken ?? '',
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.#connection.on('presenceUpdated', (entries: PresenceEntry[]) => this.presence.set(entries));

      // Rejoin the current ticket after a reconnect — the server-side group membership does not
      // survive a dropped connection.
      this.#connection.onreconnected(() => {
        const ticketId = this.#currentTicketId;
        if (ticketId) void this.#invoke('JoinTicket', ticketId);
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
