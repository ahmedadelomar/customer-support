import { Component, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { TicketPrioritiesService } from '../../data-access/ticket-priorities.service';
import type { TicketPriorityAdmin } from '../../data-access/interfaces/ticket-priority.interface';
import { PriorityFormDialogComponent } from '../../ui/priority-form-dialog/priority-form-dialog.component';

/**
 * Priority scale editor (Ticket Management / Categories and priorities). Ordered by `Level`, highest
 * urgency last in the list but shown top-to-bottom in that order so "move up" reads as "more urgent".
 */
@Component({
  selector: 'app-priority-scale',
  imports: [TranslatePipe, PageHeaderComponent, PriorityFormDialogComponent],
  templateUrl: './priority-scale.page.html',
})
export class PriorityScalePage {
  readonly #service = inject(TicketPrioritiesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly priorities = signal<TicketPriorityAdmin[]>([]);
  readonly loading = signal(true);

  readonly formOpen = signal(false);
  readonly editingPriority = signal<TicketPriorityAdmin | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (priorities) => {
        this.priorities.set(priorities);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(priority: TicketPriorityAdmin): string {
    return this.#language.pick({ en: priority.nameEn, ar: priority.nameAr });
  }

  openCreate(): void {
    this.editingPriority.set(null);
    this.formOpen.set(true);
  }

  openEdit(priority: TicketPriorityAdmin): void {
    this.editingPriority.set(priority);
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
    if (index === this.priorities().length - 1) return;
    this.#swapAndReorder(index, index + 1);
  }

  delete(priority: TicketPriorityAdmin): void {
    if (!confirm(`Delete "${this.name(priority)}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(priority.id).subscribe({
      next: () => {
        this.#toast.success('admin.priorities.deleted');
        this.load();
      },
    });
  }

  #swapAndReorder(a: number, b: number): void {
    const items = [...this.priorities()];
    [items[a], items[b]] = [items[b], items[a]];
    this.priorities.set(items);

    this.#service.reorder(items.map((p) => p.id)).subscribe({
      error: () => this.load(),
    });
  }
}
