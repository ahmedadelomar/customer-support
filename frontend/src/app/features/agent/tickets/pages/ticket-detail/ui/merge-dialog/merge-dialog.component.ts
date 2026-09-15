import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../../../data-access/tickets.service';

/**
 * Merges the current ticket into another, found by its human-readable number — the id the API
 * needs is an implementation detail the agent should never have to know.
 */
@Component({
  selector: 'app-merge-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './merge-dialog.component.html',
})
export class MergeDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();

  readonly merged = output<void>();
  readonly closed = output<void>();

  readonly targetNumber = signal('');
  readonly reason = signal('');
  readonly allowCrossCustomer = signal(false);
  readonly saving = signal(false);
  readonly errorKey = signal<string | null>(null);

  ngOnChanges(): void {
    this.targetNumber.set('');
    this.reason.set('');
    this.allowCrossCustomer.set(false);
    this.errorKey.set(null);
  }

  close(): void {
    this.closed.emit();
  }

  confirm(): void {
    const number = this.targetNumber().trim();
    if (!number) return;

    this.saving.set(true);
    this.errorKey.set(null);

    this.#service.list({ page: 1, pageSize: 1, search: number }).subscribe({
      next: (result) => {
        const target = result.items.find((t) => t.number.toLowerCase() === number.toLowerCase());
        if (!target) {
          this.saving.set(false);
          this.errorKey.set('tickets.merge.notFound');
          return;
        }

        this.#service
          .merge(this.ticketId(), {
            targetTicketId: target.id,
            reason: this.reason() || undefined,
            allowCrossCustomer: this.allowCrossCustomer(),
          })
          .subscribe({
            next: () => {
              this.saving.set(false);
              this.#toast.success('tickets.merged');
              this.merged.emit();
            },
            error: () => this.saving.set(false),
          });
      },
      error: () => this.saving.set(false),
    });
  }
}
