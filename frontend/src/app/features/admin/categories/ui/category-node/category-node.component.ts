import { Component, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import type { CategoryDefaultOption, TicketCategoryNode } from '../../data-access/interfaces/ticket-category.interface';

/**
 * One row of the category tree, rendering its own children recursively — the natural shape for a
 * tree of unknown depth. Self-imports its own selector for the recursive `@for` in the template.
 */
@Component({
  selector: 'app-category-node',
  imports: [TranslatePipe, CategoryNodeComponent],
  templateUrl: './category-node.component.html',
})
export class CategoryNodeComponent {
  readonly #language = inject(LanguageService);

  readonly node = input.required<TicketCategoryNode>();
  readonly priorityOptions = input<CategoryDefaultOption[]>([]);
  readonly departmentOptions = input<CategoryDefaultOption[]>([]);

  readonly addChild = output<string>();
  readonly edit = output<TicketCategoryNode>();
  readonly move = output<TicketCategoryNode>();
  readonly toggleActive = output<TicketCategoryNode>();

  readonly expanded = signal(true);

  name(): string {
    const n = this.node();
    return this.#language.pick({ en: n.nameEn, ar: n.nameAr });
  }

  defaultPriorityName(): string | null {
    const id = this.node().defaultPriorityId;
    if (!id) return null;
    const option = this.priorityOptions().find((p) => p.id === id);
    return option ? this.#language.pick({ en: option.nameEn, ar: option.nameAr }) : null;
  }

  defaultDepartmentName(): string | null {
    const id = this.node().defaultDepartmentId;
    if (!id) return null;
    const option = this.departmentOptions().find((d) => d.id === id);
    return option ? this.#language.pick({ en: option.nameEn, ar: option.nameAr }) : null;
  }

  toggleExpanded(): void {
    this.expanded.update((v) => !v);
  }
}
