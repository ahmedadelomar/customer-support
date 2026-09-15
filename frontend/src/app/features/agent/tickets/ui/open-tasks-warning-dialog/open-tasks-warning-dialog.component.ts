import { Component, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../data-access/tickets.service';
import type { OpenTaskSummary } from '../../data-access/interfaces/ticket.interface';

/**
 * Shown when moving a ticket to a terminal status is refused because it still has open linked tasks
 * (`OpenTasksWarningException`, 409). Offers "close anyway" (leaves the tasks open) or "close and
 * complete them" — both resubmit the same status change with the matching flag set.
 */
@Component({
  selector: 'app-open-tasks-warning-dialog',
  imports: [TranslatePipe, ModalComponent],
  templateUrl: './open-tasks-warning-dialog.component.html',
})
export class OpenTasksWarningDialogComponent {
  readonly #service = inject(TicketsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();
  readonly statusId = input<string>('');
  readonly resolutionNote = input<string | undefined>(undefined);
  readonly openTasks = input<OpenTaskSummary[]>([]);

  readonly resolved = output<void>();
  readonly closed = output<void>();

  readonly saving = signal(false);

  close(): void {
    this.closed.emit();
  }

  closeAnyway(): void {
    this.#submit(true, false);
  }

  closeAndComplete(): void {
    this.#submit(false, true);
  }

  #submit(force: boolean, completeLinkedTasks: boolean): void {
    this.saving.set(true);

    this.#service
      .changeStatus(this.ticketId(), {
        statusId: this.statusId(),
        resolutionNote: this.resolutionNote(),
        force,
        completeLinkedTasks,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.#toast.success('tickets.properties.statusChanged');
          this.resolved.emit();
        },
        error: () => this.saving.set(false),
      });
  }
}
