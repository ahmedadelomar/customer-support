import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { AuthService } from '../../../../../core/auth/auth.service';
import { NotificationChannel } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TasksService, type TaskPriorityOption } from '../../data-access/tasks.service';
import type { AgentTask } from '../../data-access/interfaces/task.interface';

const CHANNEL_OPTIONS: { value: NotificationChannel; labelKey: string }[] = [
  { value: NotificationChannel.InApp, labelKey: 'tasks.channels.inApp' },
  { value: NotificationChannel.Email, labelKey: 'tasks.channels.email' },
  { value: NotificationChannel.Sms, labelKey: 'tasks.channels.sms' },
];

/**
 * The compact task composer (Agent Dashboard / Tasks and reminders): title, due date-time, priority,
 * and an optional reminder that fires at the same due time — reused for creating a standalone task,
 * a ticket-linked task (inline add on the ticket screen) and editing an existing one. Assigning to a
 * colleague is not exposed here: no agent-directory-search component exists in this codebase to pick
 * one from, so the composer always creates/edits the caller's own task (the backend itself supports
 * any assignee — see `CreateTaskCommand`).
 */
@Component({
  selector: 'app-task-form-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './task-form-dialog.component.html',
})
export class TaskFormDialogComponent implements OnChanges {
  readonly #service = inject(TasksService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #auth = inject(AuthService);

  readonly open = input(false);
  readonly task = input<AgentTask | null>(null);
  readonly ticketId = input<string | null>(null);
  readonly customerId = input<string | null>(null);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly channelOptions = CHANNEL_OPTIONS;

  readonly title = signal('');
  readonly description = signal('');
  readonly dueAtLocal = signal('');
  readonly priorityId = signal('');
  readonly withReminder = signal(false);
  readonly reminderChannels = signal<NotificationChannel[]>([NotificationChannel.InApp]);
  readonly priorities = signal<TaskPriorityOption[]>([]);
  readonly saving = signal(false);
  readonly errors = signal<Record<string, string[]>>({});

  ngOnChanges(): void {
    if (!this.open()) {
      return;
    }

    this.errors.set({});
    this.#service.priorityOptions().subscribe((priorities) => this.priorities.set(priorities));

    const existing = this.task();
    if (existing) {
      this.title.set(existing.title);
      this.description.set(existing.description ?? '');
      this.dueAtLocal.set(existing.dueAt ? this.#toLocalInputValue(existing.dueAt) : '');
      this.priorityId.set(existing.priorityId ?? '');
      this.withReminder.set(false);
    } else {
      this.title.set('');
      this.description.set('');
      this.dueAtLocal.set('');
      this.priorityId.set('');
      this.withReminder.set(false);
      this.reminderChannels.set([NotificationChannel.InApp]);
    }
  }

  priorityName(priority: TaskPriorityOption): string {
    return this.#language.pick({ en: priority.nameEn, ar: priority.nameAr });
  }

  isChannelChecked(channel: NotificationChannel): boolean {
    return this.reminderChannels().includes(channel);
  }

  toggleChannel(channel: NotificationChannel, checked: boolean): void {
    this.reminderChannels.update((current) =>
      checked ? [...current, channel] : current.filter((c) => c !== channel),
    );
  }

  close(): void {
    this.closed.emit();
  }

  save(): void {
    if (!this.title().trim()) {
      this.errors.set({ title: ['Title is required.'] });
      return;
    }

    this.saving.set(true);
    this.errors.set({});

    const existing = this.task();
    const dueAtIso = this.dueAtLocal() ? new Date(this.dueAtLocal()).toISOString() : undefined;

    const request$: Observable<string | void> = existing
      ? this.#service.update({
          id: existing.id,
          assignedToId: existing.assignedToId,
          title: this.title(),
          description: this.description() || undefined,
          priorityId: this.priorityId() || undefined,
          dueAt: dueAtIso,
        })
      : this.#service.create({
          ticketId: this.ticketId() ?? undefined,
          customerId: this.customerId() ?? undefined,
          assignedToId: this.#auth.user()?.id ?? '',
          title: this.title(),
          description: this.description() || undefined,
          priorityId: this.priorityId() || undefined,
          dueAt: dueAtIso,
          reminder:
            this.withReminder() && this.dueAtLocal()
              ? { remindAtLocal: this.dueAtLocal(), channels: this.reminderChannels() }
              : undefined,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'tasks.updated' : 'tasks.created');
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errors.set(this.#extractFieldErrors(error));
      },
    });
  }

  #toLocalInputValue(iso: string): string {
    const date = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  #extractFieldErrors(error: unknown): Record<string, string[]> {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { errors?: Record<string, string[]> } }).error;
      if (problem?.errors) return problem.errors;
    }
    return {};
  }
}
