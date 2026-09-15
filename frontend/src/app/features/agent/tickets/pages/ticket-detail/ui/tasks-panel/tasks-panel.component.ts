import { Component, type OnChanges, computed, inject, input, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AgentTaskStatus } from '../../../../../../../core/models/enums';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../../../shared/ui/empty-state/empty-state.component';
import { relativeTime } from '../../../../../../../shared/utils/relative-time';
import { TasksService } from '../../../../../tasks/data-access/tasks.service';
import type { AgentTask } from '../../../../../tasks/data-access/interfaces/task.interface';
import { TaskFormDialogComponent } from '../../../../../tasks/pages/task-list/ui/task-form-dialog/task-form-dialog.component';

/**
 * Ticket screen tasks panel (Agent Dashboard / Tasks and reminders): tasks linked to this ticket,
 * with an inline "add task" so a follow-up never means leaving the ticket.
 */
@Component({
  selector: 'app-tasks-panel',
  imports: [TranslatePipe, EmptyStateComponent, TaskFormDialogComponent],
  templateUrl: './tasks-panel.component.html',
})
export class TasksPanelComponent implements OnChanges {
  readonly #service = inject(TasksService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly ticketId = input.required<string>();

  readonly AgentTaskStatus = AgentTaskStatus;

  readonly items = signal<AgentTask[]>([]);
  readonly loading = signal(true);
  readonly formOpen = signal(false);

  readonly locale = computed(() => this.#language.locale());

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.ticketTasks(this.ticketId()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  openCreate(): void {
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  onSaved(): void {
    this.formOpen.set(false);
    this.load();
  }

  complete(task: AgentTask): void {
    this.#service.complete(task.id).subscribe({
      next: () => {
        this.#toast.success('tasks.completed');
        this.load();
      },
    });
  }
}
