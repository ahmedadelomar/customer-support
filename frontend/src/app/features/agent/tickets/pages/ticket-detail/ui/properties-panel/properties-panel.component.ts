import { DatePipe } from '@angular/common';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { TicketsService } from '../../../../data-access/tickets.service';
import type { TicketLookups } from '../../../../data-access/interfaces/ticket-lookups.interface';
import type { TicketDetail } from '../../../../data-access/interfaces/ticket.interface';

/**
 * Right column: ticket properties. Category and priority are editable inline through
 * `UpdateTicketCommand`; status, assignee and department are read-only here — they move through
 * their own endpoints (status workflow, CS-502 assignment, CS-1203 transfer) because each carries
 * side effects a generic property edit must not trigger.
 */
@Component({
  selector: 'app-properties-panel',
  imports: [FormsModule, TranslatePipe, DatePipe],
  templateUrl: './properties-panel.component.html',
})
export class PropertiesPanelComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly ticket = input.required<TicketDetail>();
  readonly lookups = input<TicketLookups | null>(null);

  readonly saved = output<void>();

  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly categoryId = signal('');
  readonly priorityId = signal('');

  readonly locale = computed(() => this.#language.locale());
  readonly lang = computed(() => this.#language.current());

  ngOnChanges(): void {
    if (!this.editing()) {
      this.categoryId.set(this.ticket().categoryId);
      this.priorityId.set(this.ticket().priorityId);
    }
  }

  categoryName(): string {
    const t = this.ticket();
    return this.#language.pick({ en: t.categoryNameEn, ar: t.categoryNameAr });
  }

  priorityName(): string {
    const t = this.ticket();
    return this.#language.pick({ en: t.priorityNameEn, ar: t.priorityNameAr });
  }

  statusName(): string {
    const t = this.ticket();
    return this.#language.pick({ en: t.statusNameEn, ar: t.statusNameAr });
  }

  departmentName(): string | null {
    const t = this.ticket();
    if (!t.departmentNameEn && !t.departmentNameAr) return null;
    return this.#language.pick({ en: t.departmentNameEn ?? '', ar: t.departmentNameAr ?? '' });
  }

  assigneeName(): string | null {
    const t = this.ticket();
    if (!t.assignedAgentId) return null;
    return this.#language.pick({ en: t.assignedAgentNameEn ?? '', ar: t.assignedAgentNameAr ?? '' });
  }

  startEdit(): void {
    this.categoryId.set(this.ticket().categoryId);
    this.priorityId.set(this.ticket().priorityId);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
  }

  save(): void {
    const t = this.ticket();
    this.saving.set(true);

    this.#service
      .update({
        id: t.id,
        subject: t.subject,
        description: t.description,
        categoryId: this.categoryId(),
        priorityId: this.priorityId(),
        tagIds: t.tags.map((tag) => tag.id),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.editing.set(false);
          this.#toast.success('tickets.updated');
          this.saved.emit();
        },
        error: () => this.saving.set(false),
      });
  }
}
