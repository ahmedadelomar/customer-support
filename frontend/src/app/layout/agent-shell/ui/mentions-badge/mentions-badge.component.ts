import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../core/auth/auth.service';
import { PERMISSIONS } from '../../../../core/permissions';
import { CollaborationService } from '../../../../features/agent/collaboration/data-access/collaboration.service';

const POLL_INTERVAL_MS = 30_000;

/**
 * Unread @mentions badge in the top bar (Agent Dashboard / Team collaboration) — polls the same way
 * `ReminderToastComponent` (CS-403) polls pending reminders, since there is no push channel for this
 * yet outside the collaboration hub's own ticket-scoped presence/composing signals.
 */
@Component({
  selector: 'app-mentions-badge',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './mentions-badge.component.html',
})
export class MentionsBadgeComponent {
  readonly #service = inject(CollaborationService);
  readonly #auth = inject(AuthService);
  readonly #destroyRef = inject(DestroyRef);

  readonly canCollaborate = computed(() => this.#auth.hasPermission(PERMISSIONS.workspace.collaborate));
  readonly unreadCount = signal(0);

  #intervalId: ReturnType<typeof setInterval>;

  constructor() {
    this.#poll();
    this.#intervalId = setInterval(() => this.#poll(), POLL_INTERVAL_MS);
    this.#destroyRef.onDestroy(() => clearInterval(this.#intervalId));
  }

  #poll(): void {
    if (!this.canCollaborate()) {
      return;
    }

    this.#service.mentions().subscribe({
      next: (mentions) => this.unreadCount.set(mentions.filter((m) => !m.readAt).length),
    });
  }
}
