import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TicketsService } from '../../data-access/tickets.service';
import type { AssignableAgent } from '../../data-access/interfaces/ticket.interface';

/**
 * Assignee picker for one ticket. Candidates show their current load and availability inline
 * (`Sara — 12/25 · Away`); picking one that is dimmed (Away or at capacity) opens a confirm step
 * that explains the warning and, on confirm, resends with `force: true`. A deactivated agent has no
 * confirm step at all — the server refuses it outright regardless of `force`.
 */
@Component({
  selector: 'app-assign-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './assign-dialog.component.html',
})
export class AssignDialogComponent implements OnChanges {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly ticketId = input.required<string>();

  readonly assigned = output<void>();
  readonly closed = output<void>();

  readonly candidates = signal<AssignableAgent[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly confirming = signal<AssignableAgent | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly handoverNote = signal('');

  ngOnChanges(): void {
    if (!this.open()) {
      return;
    }

    this.confirming.set(null);
    this.errorMessage.set(null);
    this.handoverNote.set('');
    this.loading.set(true);

    this.#service.assignableAgents(this.ticketId()).subscribe({
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

  select(agent: AssignableAgent): void {
    if (agent.isAvailable) {
      this.#assign(agent.id, false);
      return;
    }

    // Dimmed candidates need an explicit confirm step before resending with force — see the class remark.
    this.confirming.set(agent);
  }

  confirmWarned(): void {
    const agent = this.confirming();
    if (!agent) return;
    this.#assign(agent.id, true);
  }

  cancelConfirm(): void {
    this.confirming.set(null);
  }

  close(): void {
    this.closed.emit();
  }

  #assign(agentId: string, force: boolean): void {
    this.saving.set(true);
    this.errorMessage.set(null);

    this.#service.assign(this.ticketId(), { agentId, force, handoverNote: this.handoverNote().trim() || undefined }).subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success('tickets.assignment.assigned');
        this.assigned.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.confirming.set(null);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  #extractMessage(error: unknown): string {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { detail?: string } }).error;
      if (problem?.detail) return problem.detail;
    }
    return '';
  }
}
