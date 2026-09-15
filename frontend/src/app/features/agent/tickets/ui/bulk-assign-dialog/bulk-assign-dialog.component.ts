import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../data-access/tickets.service';
import type { AssignableAgent, BulkAssignResult } from '../../data-access/interfaces/ticket.interface';

/**
 * Assigns every selected ticket to one agent. Candidates come from the first selected ticket's
 * assignable-agents list — a deliberate simplification: bulk-assigning is normally done from one
 * filtered queue (one team or department), so the first ticket's candidates are representative of
 * the whole selection in practice.
 */
@Component({
  selector: 'app-bulk-assign-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './bulk-assign-dialog.component.html',
})
export class BulkAssignDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);

  readonly open = input(false);
  readonly ticketIds = input<string[]>([]);

  readonly closed = output<void>();
  /** Emitted once the batch call returns, whether every ticket succeeded or not — the parent reloads the list either way. */
  readonly completed = output<void>();

  readonly candidates = signal<AssignableAgent[]>([]);
  readonly loading = signal(true);
  readonly selectedAgentId = signal('');
  readonly force = signal(false);
  readonly saving = signal(false);
  readonly result = signal<BulkAssignResult | null>(null);
  readonly showFailures = signal(false);

  ngOnChanges(): void {
    if (!this.open()) {
      return;
    }

    this.result.set(null);
    this.showFailures.set(false);
    this.selectedAgentId.set('');
    this.force.set(false);
    this.loading.set(true);

    const [firstTicketId] = this.ticketIds();
    if (!firstTicketId) {
      this.loading.set(false);
      return;
    }

    this.#service.assignableAgents(firstTicketId).subscribe({
      next: (candidates) => {
        this.candidates.set(candidates);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(agent: AssignableAgent): string {
    return this.#language.pick({ en: agent.nameEn, ar: agent.nameAr });
  }

  loadLabel(agent: AssignableAgent): string {
    return agent.cap === null ? `${agent.openTickets}` : `${agent.openTickets}/${agent.cap}`;
  }

  submit(): void {
    if (!this.selectedAgentId()) {
      return;
    }

    this.saving.set(true);

    this.#service
      .bulkAssign({ ticketIds: this.ticketIds(), agentId: this.selectedAgentId(), force: this.force() })
      .subscribe({
        next: (result) => {
          this.saving.set(false);
          this.result.set(result);
          this.completed.emit();
        },
        error: () => this.saving.set(false),
      });
  }

  toggleFailures(): void {
    this.showFailures.update((v) => !v);
  }

  close(): void {
    this.closed.emit();
  }
}
