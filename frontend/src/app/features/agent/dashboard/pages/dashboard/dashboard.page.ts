import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../../core/auth/auth.service';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { StateCardComponent } from '../../../../../shared/ui/state-card/state-card.component';
import { AgentDashboardService } from '../../data-access/agent-dashboard.service';
import type { AgentDashboard } from '../../data-access/interfaces/agent-dashboard.interface';
import { NextUpQueueComponent } from './ui/next-up-queue/next-up-queue.component';

const REFRESH_INTERVAL_MS = 30_000;

const EMPTY_DASHBOARD: AgentDashboard = {
  canViewQueue: false,
  canViewStats: false,
  tiles: { assigned: 0, dueToday: 0, approachingSla: 0, breached: 0 },
  queue: [],
  stats: { resolvedThisWeek: 0, teamAverageResolvedThisWeek: 0 },
};

/**
 * Agent home (Agent Dashboard / Assigned tickets, CS-401): tiles, the urgency-ordered next-up queue
 * and personal stats, loaded in one request and kept fresh by a non-disruptive poll of just the
 * queue (see `#refreshQueue` — tiles and stats are not re-fetched on the interval, only on a full
 * `load()`). The queue's own rows have no dropdown menu (just an "Open" link), so there is no
 * open-menu state a refresh could disturb; scroll position and everything else on the page survive
 * a refresh because only the `queue` array is replaced, not the whole dashboard.
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, TranslatePipe, HasPermissionDirective, PageHeaderComponent, StateCardComponent, NextUpQueueComponent],
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  readonly #service = inject(AgentDashboardService);
  readonly #auth = inject(AuthService);
  readonly #language = inject(LanguageService);
  readonly #document = inject(DOCUMENT);
  readonly #destroyRef = inject(DestroyRef);

  readonly permissions = PERMISSIONS;

  readonly dashboard = signal<AgentDashboard>(EMPTY_DASHBOARD);
  readonly loading = signal(true);

  readonly tiles = computed(() => this.dashboard().tiles);
  readonly queue = computed(() => this.dashboard().queue);
  readonly stats = computed(() => this.dashboard().stats);
  readonly canViewQueue = computed(() => this.dashboard().canViewQueue);
  readonly canViewStats = computed(() => this.dashboard().canViewStats);

  /** Rounded to one decimal — an Angular pipe can't be nested inside a translate param object literal. */
  readonly teamAverageRounded = computed(() => Math.round(this.stats().teamAverageResolvedThisWeek * 10) / 10);

  #intervalId: ReturnType<typeof setInterval> | undefined;

  constructor() {
    this.load();

    if (!this.#document.hidden) {
      this.#startPolling();
    }

    this.#document.addEventListener('visibilitychange', this.#onVisibilityChange);
    this.#destroyRef.onDestroy(() => {
      this.#stopPolling();
      this.#document.removeEventListener('visibilitychange', this.#onVisibilityChange);
    });
  }

  displayName(): string {
    const user = this.#auth.user();
    return user ? this.#language.pick({ en: user.displayNameEn, ar: user.displayNameAr }) : '';
  }

  load(): void {
    this.loading.set(true);
    this.#service.get().subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  #startPolling(): void {
    this.#stopPolling();
    this.#intervalId = setInterval(() => this.#refreshQueue(), REFRESH_INTERVAL_MS);
  }

  #stopPolling(): void {
    if (this.#intervalId !== undefined) {
      clearInterval(this.#intervalId);
      this.#intervalId = undefined;
    }
  }

  #refreshQueue(): void {
    if (!this.canViewQueue()) return;

    this.#service.queue().subscribe({
      next: (queue) => this.dashboard.update((current) => ({ ...current, queue })),
    });
  }

  /** An unattended dashboard should not poll all night — pause while backgrounded, refresh once on return. */
  #onVisibilityChange = (): void => {
    if (this.#document.hidden) {
      this.#stopPolling();
    } else {
      this.#refreshQueue();
      this.#startPolling();
    }
  };
}
