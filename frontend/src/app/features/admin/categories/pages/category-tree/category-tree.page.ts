import { Component, computed, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { TicketCategoriesService } from '../../data-access/ticket-categories.service';
import type {
  CategoryDefaultOption,
  TicketCategoryAdmin,
  TicketCategoryNode,
} from '../../data-access/interfaces/ticket-category.interface';
import { CategoryNodeComponent } from './ui/category-node/category-node.component';
import { CategoryFormDialogComponent } from './ui/category-form-dialog/category-form-dialog.component';
import { MoveCategoryDialogComponent } from './ui/move-category-dialog/move-category-dialog.component';

/**
 * Category tree editor (Ticket Management / Categories and priorities). The flat, path-ordered list
 * from the server is built into a tree client-side — `Path` already guarantees a parent sorts before
 * its children, so one pass is enough.
 */
@Component({
  selector: 'app-category-tree',
  imports: [TranslatePipe, PageHeaderComponent, CategoryNodeComponent, CategoryFormDialogComponent, MoveCategoryDialogComponent],
  templateUrl: './category-tree.page.html',
})
export class CategoryTreePage {
  readonly #service = inject(TicketCategoriesService);

  readonly categories = signal<TicketCategoryAdmin[]>([]);
  readonly loading = signal(true);
  readonly priorityOptions = signal<CategoryDefaultOption[]>([]);
  readonly departmentOptions = signal<CategoryDefaultOption[]>([]);

  readonly formOpen = signal(false);
  readonly editingCategory = signal<TicketCategoryAdmin | null>(null);
  readonly newCategoryParentId = signal<string | null>(null);

  readonly moveDialogOpen = signal(false);
  readonly movingCategory = signal<TicketCategoryAdmin | null>(null);

  readonly tree = computed<TicketCategoryNode[]>(() => this.#buildTree(this.categories()));

  constructor() {
    this.load();
    this.#service.defaultOptions().subscribe((options) => {
      this.priorityOptions.set(options.priorities);
      this.departmentOptions.set(options.departments);
    });
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  #buildTree(flat: TicketCategoryAdmin[]): TicketCategoryNode[] {
    const nodes = new Map<string, TicketCategoryNode>(flat.map((c) => [c.id, { ...c, children: [] }]));
    const roots: TicketCategoryNode[] = [];

    for (const node of nodes.values()) {
      if (node.parentId && nodes.has(node.parentId)) {
        nodes.get(node.parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    }

    return roots;
  }

  openCreate(parentId: string | null): void {
    this.editingCategory.set(null);
    this.newCategoryParentId.set(parentId);
    this.formOpen.set(true);
  }

  openEdit(category: TicketCategoryAdmin): void {
    this.editingCategory.set(category);
    this.newCategoryParentId.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  onFormSaved(): void {
    this.formOpen.set(false);
    this.load();
  }

  openMove(category: TicketCategoryAdmin): void {
    this.movingCategory.set(category);
    this.moveDialogOpen.set(true);
  }

  closeMove(): void {
    this.moveDialogOpen.set(false);
  }

  onMoved(): void {
    this.moveDialogOpen.set(false);
    this.load();
  }

  toggleActive(category: TicketCategoryAdmin): void {
    const request$ = category.isActive ? this.#service.deactivate(category.id) : this.#service.activate(category.id);
    request$.subscribe({ next: () => this.load() });
  }

  /** Descendant count for a node, used by the move dialog's confirmation copy. */
  descendantCount(category: TicketCategoryAdmin): number {
    return this.categories().filter((c) => c.id !== category.id && c.path.startsWith(category.path)).length;
  }
}
