import { Component, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { TicketStatusesService } from '../../data-access/ticket-statuses.service';
import type { TicketStatusAdmin } from '../../data-access/interfaces/ticket-status.interface';
import { StatusFormDialogComponent } from './ui/status-form-dialog/status-form-dialog.component';

/**
 * Status workflow editor (Ticket Management / Status workflow and escalation). Mirrors the priority
 * scale editor: an ordered list, drag-to-reorder replaced with explicit ▲/▼ buttons (no drag library
 * in this codebase — see [[ticket-management-feature-progress]]).
 */
@Component({
  selector: 'app-status-workflow',
  imports: [TranslatePipe, PageHeaderComponent, StatusFormDialogComponent],
  templateUrl: './status-workflow.page.html',
})
export class StatusWorkflowPage {
  readonly #service = inject(TicketStatusesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly statuses = signal<TicketStatusAdmin[]>([]);
  readonly loading = signal(true);

  readonly formOpen = signal(false);
  readonly editingStatus = signal<TicketStatusAdmin | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (statuses) => {
        this.statuses.set(statuses);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(status: TicketStatusAdmin): string {
    return this.#language.pick({ en: status.nameEn, ar: status.nameAr });
  }

  openCreate(): void {
    this.editingStatus.set(null);
    this.formOpen.set(true);
  }

  openEdit(status: TicketStatusAdmin): void {
    this.editingStatus.set(status);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  onFormSaved(): void {
    this.formOpen.set(false);
    this.load();
  }

  moveUp(index: number): void {
    if (index === 0) return;
    this.#swapAndReorder(index, index - 1);
  }

  moveDown(index: number): void {
    if (index === this.statuses().length - 1) return;
    this.#swapAndReorder(index, index + 1);
  }

  delete(status: TicketStatusAdmin): void {
    if (!confirm(`Delete "${this.name(status)}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(status.id).subscribe({
      next: () => {
        this.#toast.success('admin.statuses.deleted');
        this.load();
      },
    });
  }

  #swapAndReorder(a: number, b: number): void {
    const items = [...this.statuses()];
    [items[a], items[b]] = [items[b], items[a]];
    this.statuses.set(items);

    this.#service.reorder(items.map((s) => s.id)).subscribe({
      error: () => this.load(),
    });
  }
}
