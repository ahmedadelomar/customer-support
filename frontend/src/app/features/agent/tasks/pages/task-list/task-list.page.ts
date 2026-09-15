import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { AgentTaskStatus } from '../../../../../core/models/enums';
import { PERMISSIONS } from '../../../../../core/permissions';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { PaginationComponent } from '../../../../../shared/ui/pagination/pagination.component';
import { relativeTime } from '../../../../../shared/utils/relative-time';
import { TasksService } from '../../data-access/tasks.service';
import type { AgentTask, TaskQuery } from '../../data-access/interfaces/task.interface';
import { TaskFormDialogComponent } from './ui/task-form-dialog/task-form-dialog.component';

/**
 * The dedicated tasks page (Agent Dashboard / Tasks and reminders). Filters for status and due
 * range; the assignee filter is not built here — it would need an agent-directory-search component
 * this codebase doesn't have yet (same gap the composer's assignee picker has).
 */
@Component({
  selector: 'app-task-list',
  imports: [
    FormsModule,
    TranslatePipe,
    HasPermissionDirective,
    PageHeaderComponent,
    PaginationComponent,
    EmptyStateComponent,
    TaskFormDialogComponent,
  ],
  templateUrl: './task-list.page.html',
})
export class TaskListPage {
  readonly #service = inject(TasksService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly permissions = PERMISSIONS;
  readonly AgentTaskStatus = AgentTaskStatus;

  readonly items = signal<AgentTask[]>([]);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);

  readonly statusFilter = signal<AgentTaskStatus | ''>('');
  readonly dueFrom = signal('');
  readonly dueTo = signal('');

  readonly formOpen = signal(false);
  readonly editingTask = signal<AgentTask | null>(null);

  readonly statusOptions = [
    { value: '' as const, labelKey: 'tasks.filters.allStatuses' },
    { value: AgentTaskStatus.Pending, labelKey: 'enums.agentTaskStatus.0' },
    { value: AgentTaskStatus.InProgress, labelKey: 'enums.agentTaskStatus.1' },
    { value: AgentTaskStatus.Completed, labelKey: 'enums.agentTaskStatus.2' },
    { value: AgentTaskStatus.Cancelled, labelKey: 'enums.agentTaskStatus.3' },
  ];

  readonly locale = computed(() => this.#language.locale());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.#service.list(this.#query()).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  customerOrTicketLabel(task: AgentTask): string | null {
    if (task.ticketNumber) return task.ticketNumber;
    if (task.customerDisplayNameEn || task.customerDisplayNameAr) {
      return this.#language.pick({ en: task.customerDisplayNameEn ?? '', ar: task.customerDisplayNameAr ?? '' });
    }
    return null;
  }

  priorityName(task: AgentTask): string | null {
    if (!task.priorityNameEn && !task.priorityNameAr) return null;
    return this.#language.pick({ en: task.priorityNameEn ?? '', ar: task.priorityNameAr ?? '' });
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  openCreate(): void {
    this.editingTask.set(null);
    this.formOpen.set(true);
  }

  openEdit(task: AgentTask): void {
    this.editingTask.set(task);
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

  delete(task: AgentTask): void {
    this.#service.delete(task.id).subscribe({
      next: () => {
        this.#toast.success('tasks.deleted');
        this.load();
      },
    });
  }

  #query(): TaskQuery {
    const status = this.statusFilter();

    return {
      status: status === '' ? undefined : status,
      dueFrom: this.dueFrom() || undefined,
      dueTo: this.dueTo() || undefined,
      page: this.page(),
      pageSize: this.pageSize,
    };
  }
}
