import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../../../data-access/tickets.service';

/** Manual escalation. Requires a reason; the department manager and any existing watchers are notified. */
@Component({
  selector: 'app-escalate-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './escalate-dialog.component.html',
})
export class EscalateDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();
  readonly departmentName = input<string | null>(null);

  readonly escalated = output<void>();
  readonly closed = output<void>();

  readonly reason = signal('');
  readonly saving = signal(false);

  ngOnChanges(): void {
    this.reason.set('');
  }

  close(): void {
    this.closed.emit();
  }

  submit(): void {
    if (!this.reason().trim()) {
      return;
    }

    this.saving.set(true);

    this.#service.escalate(this.ticketId(), { reason: this.reason() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success('tickets.escalation.escalated');
        this.escalated.emit();
      },
      error: () => this.saving.set(false),
    });
  }
}
