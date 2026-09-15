import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../../../shared/ui/modal/modal.component';
import { TicketCategoriesService } from '../../../../data-access/ticket-categories.service';
import type { TicketCategoryAdmin } from '../../../../data-access/interfaces/ticket-category.interface';

/**
 * Reparents a category. Moving a node with descendants re-paths all of them in one server call, so
 * the confirmation states how many are affected before the agent commits — this is a bulk operation
 * and should not be a surprise.
 */
@Component({
  selector: 'app-move-category-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './move-category-dialog.component.html',
})
export class MoveCategoryDialogComponent implements OnChanges {
  readonly #service = inject(TicketCategoriesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly category = input<TicketCategoryAdmin | null>(null);
  readonly allCategories = input<TicketCategoryAdmin[]>([]);
  readonly descendantCount = input(0);

  readonly moved = output<void>();
  readonly closed = output<void>();

  readonly newParentId = signal<string>('');
  readonly saving = signal(false);

  /** Every category except the node itself and anything already inside its own subtree. */
  readonly parentOptions = computed(() => {
    const current = this.category();
    if (!current) return [];

    return this.allCategories()
      .filter((c) => c.id !== current.id && !c.path.startsWith(current.path))
      .map((c) => ({ id: c.id, label: `${'—'.repeat(c.depth)} ${this.#language.pick({ en: c.nameEn, ar: c.nameAr })}` }));
  });

  ngOnChanges(): void {
    this.newParentId.set(this.category()?.parentId ?? '');
  }

  close(): void {
    this.closed.emit();
  }

  confirm(): void {
    const current = this.category();
    if (!current) return;

    this.saving.set(true);
    this.#service.move(current.id, this.newParentId() || null).subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success('admin.categories.moved');
        this.moved.emit();
      },
      error: () => this.saving.set(false),
    });
  }
}
