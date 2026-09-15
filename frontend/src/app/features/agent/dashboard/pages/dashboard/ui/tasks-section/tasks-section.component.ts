import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
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
 * Dashboard section (Agent Dashboard / Tasks and reminders): the agent's own open tasks by due
 * date, overdue ones called out in rose. A cross-feature import of the tasks feature's own
 * service/dialog — the same kind of type/component reuse CS-402 already established, just one
 * level up (a whole page importing a sibling feature's public pieces rather than duplicating them).
 */
@Component({
  selector: 'app-tasks-section',
  imports: [RouterLink, TranslatePipe, EmptyStateComponent, TaskFormDialogComponent],
  templateUrl: './tasks-section.component.html',
})
export class TasksSectionComponent {
  readonly #service = inject(TasksService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly AgentTaskStatus = AgentTaskStatus;

  readonly items = signal<AgentTask[]>([]);
  readonly loading = signal(true);
  readonly formOpen = signal(false);

  readonly locale = computed(() => this.#language.locale());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    // The list endpoint filters by one status at a time; "open" (Pending or InProgress) isn't one
    // of them, so a slightly larger page is fetched and trimmed here rather than adding a
    // multi-status filter the rest of the API doesn't otherwise need.
    this.#service.list({ pageSize: 10, page: 1 }).subscribe({
      next: (result) => {
        this.items.set(
          result.items
            .filter((t) => t.status !== AgentTaskStatus.Completed && t.status !== AgentTaskStatus.Cancelled)
            .slice(0, 5),
        );
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
