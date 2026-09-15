import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../core/services/language.service';
import { RemindersService } from '../../../../features/agent/tasks/data-access/reminders.service';
import type { PendingReminder } from '../../../../features/agent/tasks/data-access/interfaces/reminder.interface';
import { relativeTime } from '../../../../shared/utils/relative-time';

const POLL_INTERVAL_MS = 20_000;

const SNOOZE_OPTIONS: { minutes: number; labelKey: string }[] = [
  { minutes: 10, labelKey: 'reminders.snooze.tenMinutes' },
  { minutes: 60, labelKey: 'reminders.snooze.oneHour' },
  { minutes: 180, labelKey: 'reminders.snooze.threeHours' },
  { minutes: 24 * 60, labelKey: 'reminders.snooze.tomorrow' },
];

/**
 * Persistent reminder toasts (Agent Dashboard / Tasks and reminders) — separate from
 * `ToastService`'s transient messages, since a fired reminder must stay visible until the agent
 * snoozes or dismisses it, not auto-clear after a few seconds. Polls `/api/reminders/pending` on an
 * interval; a reminder already shown is not re-toasted on the next poll (tracked by id), and one
 * that gets dismissed/snoozed elsewhere (another tab) simply stops reappearing once its poll
 * response no longer includes it.
 */
@Component({
  selector: 'app-reminder-toast',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './reminder-toast.component.html',
})
export class ReminderToastComponent {
  readonly #service = inject(RemindersService);
  readonly #language = inject(LanguageService);
  readonly #destroyRef = inject(DestroyRef);

  readonly snoozeOptions = SNOOZE_OPTIONS;

  readonly reminders = signal<PendingReminder[]>([]);
  readonly openSnoozeMenuId = signal<string | null>(null);
  readonly locale = computed(() => this.#language.locale());

  #intervalId: ReturnType<typeof setInterval>;

  constructor() {
    this.#poll();
    this.#intervalId = setInterval(() => this.#poll(), POLL_INTERVAL_MS);
    this.#destroyRef.onDestroy(() => clearInterval(this.#intervalId));
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  toggleSnoozeMenu(id: string): void {
    this.openSnoozeMenuId.update((current) => (current === id ? null : id));
  }

  snooze(reminder: PendingReminder, minutes: number): void {
    this.openSnoozeMenuId.set(null);
    this.#service.snooze(reminder.id, minutes).subscribe({
      next: () => this.#remove(reminder.id),
    });
  }

  dismiss(reminder: PendingReminder): void {
    this.openSnoozeMenuId.set(null);
    this.#service.dismiss(reminder.id).subscribe({
      next: () => this.#remove(reminder.id),
    });
  }

  #poll(): void {
    this.#service.pending().subscribe({
      next: (reminders) => this.reminders.set(reminders),
    });
  }

  #remove(id: string): void {
    this.reminders.update((current) => current.filter((r) => r.id !== id));
  }
}
