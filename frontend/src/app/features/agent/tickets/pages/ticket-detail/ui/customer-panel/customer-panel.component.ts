import { Component, type OnChanges, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { CustomerNotesService } from '../../../../../customers/data-access/customer-notes.service';
import type { CustomerNote } from '../../../../../customers/data-access/interfaces/note.interface';
import type { TicketCustomerSummary } from '../../../../data-access/interfaces/ticket.interface';

/**
 * Left column of the ticket detail screen: who the ticket is for. Pinned notes are surfaced here
 * (CS-104's `CustomerNotesService`) because they carry the context an agent needs before replying —
 * VIP handling, known issues — without leaving the ticket.
 */
@Component({
  selector: 'app-customer-panel',
  imports: [RouterLink, TranslatePipe],
  templateUrl: './customer-panel.component.html',
})
export class CustomerPanelComponent implements OnChanges {
  readonly #notes = inject(CustomerNotesService);
  readonly #language = inject(LanguageService);

  readonly customer = input.required<TicketCustomerSummary>();

  readonly pinnedNotes = signal<CustomerNote[]>([]);

  readonly displayName = computed(() => {
    const c = this.customer();
    return this.#language.pick({ en: c.displayNameEn, ar: c.displayNameAr });
  });

  ngOnChanges(): void {
    this.#notes.list(this.customer().id, 1, 50).subscribe({
      next: (result) => this.pinnedNotes.set(result.items.filter((n) => n.isPinned)),
    });
  }
}
