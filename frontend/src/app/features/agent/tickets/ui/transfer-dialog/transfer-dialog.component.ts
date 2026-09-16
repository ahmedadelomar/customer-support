import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import {
  OrganizationService,
  type Department,
  type Team,
} from '../../../../admin/organization/data-access/organization.service';
import { TicketsService } from '../../data-access/tickets.service';

/**
 * Moves a ticket to another department (Platform / Departments, teams and queue scoping).
 *
 * States plainly that the current assignee will be cleared — that is the part people are surprised
 * by, and discovering it after the fact looks like the app lost the assignment.
 */
@Component({
  selector: 'app-transfer-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './transfer-dialog.component.html',
})
export class TransferDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #organization = inject(OrganizationService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();
  readonly currentDepartmentId = input<string | null>(null);
  readonly hasAssignee = input(false);

  readonly transferred = output<void>();
  readonly closed = output<void>();

  readonly departments = signal<Department[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly departmentId = signal('');
  readonly teamId = signal('');
  readonly reason = signal('');

  readonly lang = computed(() => this.#language.current());

  /** Only the destination department's teams may be picked; the server enforces the same rule. */
  readonly availableTeams = computed(() =>
    this.teams().filter((t) => t.departmentId === this.departmentId()),
  );

  ngOnChanges(): void {
    if (!this.open()) {
      return;
    }

    this.departmentId.set('');
    this.teamId.set('');
    this.reason.set('');
    this.errorMessage.set(null);

    this.#organization.departments().subscribe({
      next: (departments) =>
        // Transferring to the department it is already in is a no-op, so do not offer it.
        this.departments.set(departments.filter((d) => d.id !== this.currentDepartmentId())),
    });

    this.#organization.teams().subscribe({ next: (teams) => this.teams.set(teams) });
  }

  name(scope: { nameEn: string; nameAr: string }): string {
    return this.#language.pick({ en: scope.nameEn, ar: scope.nameAr });
  }

  onDepartmentChange(value: string): void {
    this.departmentId.set(value);
    this.teamId.set('');
  }

  submit(): void {
    if (!this.departmentId()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.#service
      .transfer(this.ticketId(), {
        departmentId: this.departmentId(),
        teamId: this.teamId() || undefined,
        reason: this.reason() || undefined,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.#toast.success('tickets.transfer.done');
          this.transferred.emit();
        },
        error: (error: unknown) => {
          this.saving.set(false);

          if (error && typeof error === 'object' && 'error' in error) {
            const problem = (error as { error?: { detail?: string } }).error;
            this.errorMessage.set(problem?.detail ?? null);
          }
        },
      });
  }

  close(): void {
    this.closed.emit();
  }
}
