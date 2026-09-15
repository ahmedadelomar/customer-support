import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../../../data-access/tickets.service';

/**
 * Resolving is the one status move that needs its own step: a resolution note is required, and a
 * satisfaction survey is queued the moment the server accepts it — the hint here is what tells the
 * agent that's about to happen.
 */
@Component({
  selector: 'app-resolve-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './resolve-dialog.component.html',
})
export class ResolveDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();
  readonly statusId = input.required<string>();

  readonly resolved = output<void>();
  readonly closed = output<void>();

  readonly note = signal('');
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnChanges(): void {
    this.note.set('');
    this.errorMessage.set(null);
  }

  close(): void {
    this.closed.emit();
  }

  submit(): void {
    if (!this.note().trim()) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.#service.changeStatus(this.ticketId(), { statusId: this.statusId(), resolutionNote: this.note() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success('tickets.resolve.resolved');
        this.resolved.emit();
      },
      error: () => this.saving.set(false),
    });
  }
}
