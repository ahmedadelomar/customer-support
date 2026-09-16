import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from '../../../core/auth/auth.service';
import { AppConfigService } from '../../../core/services/app-config.service';
import type { RealtimeNotification } from './interfaces/notification.interface';

/**
 * Client for the notifications hub (`/hubs/notifications`) — real-time push the instant a
 * notification commits server-side. A single app-wide connection (`providedIn: 'root'`), started
 * lazily the first time the bell mounts, which for a signed-in session is effectively "on sign-in"
 * without needing a separate app-initializer. On reconnect the caller should refetch the unread
 * count rather than assume the in-memory list is complete — the connection has no memory of what
 * arrived while it was down.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsHubService {
  readonly #auth = inject(AuthService);
  readonly #config = inject(AppConfigService);

  /** The most recently pushed notification, or null before the first one arrives this session. */
  readonly received = signal<RealtimeNotification | null>(null);
  /** Increments on every reconnect — a cheap trigger consumers can watch to refetch the unread count. */
  readonly reconnected = signal(0);

  #connection: HubConnection | null = null;
  #connecting: Promise<void> | null = null;

  async connect(): Promise<void> {
    if (!this.#connection) {
      this.#connection = new HubConnectionBuilder()
        .withUrl(`${this.#config.apiUrl}/hubs/notifications`, {
          accessTokenFactory: () => this.#auth.accessToken ?? '',
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      this.#connection.on('notificationReceived', (notification: RealtimeNotification) => {
        this.received.set(notification);
      });

      this.#connection.onreconnected(() => this.reconnected.update((n) => n + 1));
    }

    if (this.#connection.state === HubConnectionState.Disconnected) {
      this.#connecting ??= this.#connection.start().finally(() => {
        this.#connecting = null;
      });
      await this.#connecting;
    }
  }
}
