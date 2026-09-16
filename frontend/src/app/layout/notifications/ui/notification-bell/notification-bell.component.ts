import { Component, ElementRef, HostListener, effect, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from '../../../../core/services/language.service';
import { relativeTime } from '../../../../shared/utils/relative-time';
import { NotificationsHubService } from '../../data-access/notifications-hub.service';
import { NotificationsService } from '../../data-access/notifications.service';
import type { AppNotification } from '../../data-access/interfaces/notification.interface';

const PAGE_SIZE = 30;

/**
 * The bell button in the top bar (SLA and Automation / Alerts and notifications): unread badge,
 * a dropdown panel newest-first with severity colouring, and a live connection to the notifications
 * hub so arrivals update the badge without a refresh. Shared by both the agent and admin shells.
 */
@Component({
  selector: 'app-notification-bell',
  imports: [TranslatePipe],
  templateUrl: './notification-bell.component.html',
})
export class NotificationBellComponent {
  readonly #service = inject(NotificationsService);
  readonly #hub = inject(NotificationsHubService);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);
  readonly #router = inject(Router);

  readonly open = signal(false);
  readonly unreadCount = signal(0);
  readonly notifications = signal<AppNotification[]>([]);
  readonly loading = signal(false);
  readonly hasMore = signal(true);

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  constructor() {
    void this.#hub.connect();
    this.#refreshUnreadCount();

    // A new push updates the badge immediately; if the panel is open, it also lands at the top of
    // the visible list rather than waiting for the next open to show it.
    effect(() => {
      const arrived = this.#hub.received();
      if (!arrived) return;

      this.unreadCount.update((n) => n + 1);
      if (this.open()) {
        this.notifications.update((rows) => [{ ...arrived, readAt: null }, ...rows]);
      }
    });

    // The connection has no memory of what happened while it was down — refetch rather than trust the in-memory count.
    effect(() => {
      if (this.#hub.reconnected() > 0) {
        this.#refreshUnreadCount();
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.panel()?.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  toggle(): void {
    this.open.update((v) => !v);
    if (this.open() && this.notifications().length === 0) {
      this.loadMore();
    }
  }

  close(): void {
    this.open.set(false);
  }

  loadMore(): void {
    this.loading.set(true);
    this.#service.list(this.notifications().length, PAGE_SIZE).subscribe({
      next: (page) => {
        this.notifications.update((rows) => [...rows, ...page]);
        this.hasMore.set(page.length === PAGE_SIZE);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  title(notification: AppNotification): string {
    return this.#language.pick({ en: notification.titleEn, ar: notification.titleAr });
  }

  body(notification: AppNotification): string {
    return this.#language.pick({ en: notification.bodyEn, ar: notification.bodyAr });
  }

  timeAgo(notification: AppNotification): string {
    return relativeTime(notification.createdAt, this.#language.locale());
  }

  severityClasses(notification: AppNotification): string {
    switch (notification.severity) {
      case 'Critical':
        return 'text-rose-600';
      case 'Warning':
        return 'text-amber-600';
      default:
        return 'text-slate-500';
    }
  }

  openNotification(notification: AppNotification): void {
    if (!notification.readAt) {
      this.#markRead(notification);
    }

    this.close();
    if (notification.link) {
      void this.#router.navigateByUrl(notification.link);
    }
  }

  markAllRead(): void {
    this.#service.markAllRead().subscribe(() => {
      this.notifications.update((rows) => rows.map((n) => ({ ...n, readAt: n.readAt ?? new Date().toISOString() })));
      this.unreadCount.set(0);
    });
  }

  #markRead(notification: AppNotification): void {
    this.notifications.update((rows) =>
      rows.map((n) => (n.id === notification.id ? { ...n, readAt: new Date().toISOString() } : n)),
    );
    this.unreadCount.update((n) => Math.max(0, n - 1));

    this.#service.markRead(notification.id).subscribe({
      error: () => this.#refreshUnreadCount(), // resync on failure rather than trust the optimistic update
    });
  }

  #refreshUnreadCount(): void {
    this.#service.unreadCount().subscribe((count) => this.unreadCount.set(count));
  }
}
