import { DatePipe } from '@angular/common';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { TicketStatusKind } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketLookups, TicketStatusLookup } from '../../data-access/interfaces/ticket-lookups.interface';
import type { OpenTaskSummary, TicketDetail } from '../../data-access/interfaces/ticket.interface';

/** Payload for `openTasksWarning` — enough for the parent to open the confirm dialog and resubmit. */
export interface OpenTasksWarningEvent {
  statusId: string;
  openTasks: OpenTaskSummary[];
}

/** One status kind's group of options, for the grouped `<select>`. */
interface StatusKindGroup {
  kind: TicketStatusKind;
  labelKey: string;
  statuses: TicketStatusLookup[];
}

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
  /** The properties panel only opens these dialogs; `ticket-detail.page` owns them, alongside merge. */
  readonly assignRequested = output<void>();
  /** Emitted instead of changing the status directly, whenever the target status is Resolved-kind. */
  readonly resolveRequested = output<string>();
  readonly escalateRequested = output<void>();
  /** Emitted when the server refuses a status change because open linked tasks exist (409). */
  readonly openTasksWarning = output<OpenTasksWarningEvent>();

  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly categoryId = signal('');
  readonly priorityId = signal('');
  readonly claiming = signal(false);
  readonly unassigning = signal(false);
  readonly changingStatus = signal(false);

  readonly locale = computed(() => this.#language.locale());
  readonly lang = computed(() => this.#language.current());

  private static readonly KIND_ORDER: { kind: TicketStatusKind; labelKey: string }[] = [
    { kind: TicketStatusKind.New, labelKey: 'enums.ticketStatusKind.0' },
    { kind: TicketStatusKind.Open, labelKey: 'enums.ticketStatusKind.1' },
    { kind: TicketStatusKind.Pending, labelKey: 'enums.ticketStatusKind.2' },
    { kind: TicketStatusKind.OnHold, labelKey: 'enums.ticketStatusKind.3' },
    { kind: TicketStatusKind.Resolved, labelKey: 'enums.ticketStatusKind.4' },
    { kind: TicketStatusKind.Closed, labelKey: 'enums.ticketStatusKind.5' },
    { kind: TicketStatusKind.Cancelled, labelKey: 'enums.ticketStatusKind.6' },
  ];

  readonly statusGroups = computed<StatusKindGroup[]>(() => {
    const statuses = this.lookups()?.statuses ?? [];

    return PropertiesPanelComponent.KIND_ORDER.map((group) => ({
      ...group,
      statuses: statuses.filter((s) => s.kind === group.kind),
    })).filter((group) => group.statuses.length > 0);
  });

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

  openAssignDialog(): void {
    this.assignRequested.emit();
  }

  onStatusChange(newStatusId: string): void {
    if (!newStatusId || newStatusId === this.ticket().statusId) {
      return;
    }

    const target = (this.lookups()?.statuses ?? []).find((s) => s.id === newStatusId);

    if (target?.kind === TicketStatusKind.Resolved) {
      this.resolveRequested.emit(newStatusId);
      return;
    }

    this.changingStatus.set(true);
    this.#service.changeStatus(this.ticket().id, { statusId: newStatusId }).subscribe({
      next: () => {
        this.changingStatus.set(false);
        this.#toast.success('tickets.properties.statusChanged');
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.changingStatus.set(false);
        const openTasks = this.#extractOpenTasks(error);
        if (openTasks) {
          this.openTasksWarning.emit({ statusId: newStatusId, openTasks });
        }
      },
    });
  }

  #extractOpenTasks(error: unknown): OpenTaskSummary[] | null {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { openTasks?: OpenTaskSummary[] } }).error;
      if (problem?.openTasks) return problem.openTasks;
    }
    return null;
  }

  openEscalateDialog(): void {
    this.escalateRequested.emit();
  }

  claim(): void {
    this.claiming.set(true);
    this.#service.claim(this.ticket().id).subscribe({
      next: () => {
        this.claiming.set(false);
        this.#toast.success('tickets.assignment.claimed');
        this.saved.emit();
      },
      error: () => this.claiming.set(false),
    });
  }

  unassign(): void {
    this.unassigning.set(true);
    this.#service.unassign(this.ticket().id).subscribe({
      next: () => {
        this.unassigning.set(false);
        this.#toast.success('tickets.assignment.unassigned');
        this.saved.emit();
      },
      error: () => this.unassigning.set(false),
    });
  }
}
