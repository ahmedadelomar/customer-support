import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ChannelKey } from '../../../../../core/models/enums';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { CustomersService } from '../../../customers/data-access/customers.service';
import type { CustomerListItem } from '../../../customers/data-access/interfaces/customer.interface';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketCategoryLookup, TicketLookups } from '../../data-access/interfaces/ticket-lookups.interface';
import type { CreateTicketRequest } from '../../data-access/interfaces/ticket.interface';

/**
 * Ticket creation form (Ticket Management / Create and track tickets). The customer field is a
 * typeahead against `/api/customers`; category/priority/department mirror the server's own default
 * resolution — leaving priority/department blank lets `CreateTicketCommandHandler` apply the
 * category's defaults, so the UI never has to duplicate that fallback chain.
 */
@Component({
  selector: 'app-ticket-form',
  imports: [ReactiveFormsModule, FormsModule, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './ticket-form.page.html',
})
export class TicketFormPage {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(TicketsService);
  readonly #customers = inject(CustomersService);
  readonly #router = inject(Router);
  readonly #toast = inject(ToastService);
  readonly #language = inject(LanguageService);

  readonly saving = signal(false);
  readonly lookups = signal<TicketLookups | null>(null);

  readonly channels = [
    { value: ChannelKey.Email, labelKey: 'enums.channel.0' },
    { value: ChannelKey.WhatsApp, labelKey: 'enums.channel.1' },
    { value: ChannelKey.Sms, labelKey: 'enums.channel.3' },
    { value: ChannelKey.Phone, labelKey: 'enums.channel.6' },
    { value: ChannelKey.Portal, labelKey: 'enums.channel.5' },
    { value: ChannelKey.Internal, labelKey: 'enums.channel.8' },
  ];

  readonly categoryOptions = computed<(TicketCategoryLookup & { label: string })[]>(() =>
    (this.lookups()?.categories ?? []).map((c) => ({
      ...c,
      label: `${'—'.repeat(c.depth)} ${this.#language.pick({ en: c.nameEn, ar: c.nameAr })}`,
    })),
  );

  // --- Customer typeahead -----------------------------------------------------------------
  readonly customerSearch = signal('');
  readonly customerResults = signal<CustomerListItem[]>([]);
  readonly selectedCustomer = signal<CustomerListItem | null>(null);
  readonly searching = signal(false);
  #searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly form = this.#fb.nonNullable.group({
    subject: ['', [Validators.required, Validators.maxLength(500)]],
    description: ['', Validators.required],
    categoryId: ['', Validators.required],
    priorityId: [''],
    departmentId: [''],
    channel: [ChannelKey.Phone, Validators.required],
  });

  constructor() {
    this.#service.lookups().subscribe((lookups) => this.lookups.set(lookups));
  }

  customerName(customer: CustomerListItem): string {
    return this.#language.pick({ en: customer.displayNameEn, ar: customer.displayNameAr });
  }

  onCustomerSearchInput(term: string): void {
    this.customerSearch.set(term);

    if (this.#searchTimer) {
      clearTimeout(this.#searchTimer);
    }

    if (!term.trim()) {
      this.customerResults.set([]);
      return;
    }

    this.#searchTimer = setTimeout(() => {
      this.searching.set(true);
      this.#customers.list({ page: 1, pageSize: 8, search: term.trim() }).subscribe({
        next: (result) => {
          this.customerResults.set(result.items);
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
    }, 350);
  }

  selectCustomer(customer: CustomerListItem): void {
    this.selectedCustomer.set(customer);
    this.customerResults.set([]);
    this.customerSearch.set('');
  }

  clearCustomer(): void {
    this.selectedCustomer.set(null);
  }

  showError(name: string): boolean {
    const control = this.form.get(name);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  submit(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid || !this.selectedCustomer()) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    const request: CreateTicketRequest = {
      customerId: this.selectedCustomer()!.id,
      subject: value.subject,
      description: value.description,
      categoryId: value.categoryId,
      priorityId: value.priorityId || undefined,
      departmentId: value.departmentId || undefined,
      channel: value.channel,
    };

    this.#service.create(request).subscribe({
      next: (id) => {
        this.saving.set(false);
        this.#toast.success('tickets.created');
        void this.#router.navigate(['/agent/tickets', id]);
      },
      error: () => this.saving.set(false),
    });
  }

  cancel(): void {
    void this.#router.navigate(['/agent/tickets']);
  }
}
