import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { HasPermissionDirective } from '../../../core/directives/has-permission.directive';
import { ContactType } from '../../../core/models/enums';
import { PERMISSIONS } from '../../../core/permissions';
import { LanguageService } from '../../../core/services/language.service';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import { StateCardComponent } from '../../../shared/ui/state-card/state-card.component';
import type { CustomerContact, CustomerDetail } from './customer.models';
import { CustomersService } from './customers.service';

/**
 * Customer profile page. Contacts (Contact details) render here; the interaction-history and
 * notes tabs are added by their own stories against this same shell.
 */
@Component({
  selector: 'app-customer-detail',
  imports: [
    RouterLink,
    DatePipe,
    TranslatePipe,
    PageHeaderComponent,
    StateCardComponent,
    EmptyStateComponent,
    HasPermissionDirective,
  ],
  templateUrl: './customer-detail.page.html',
})
export class CustomerDetailPage {
  readonly #service = inject(CustomersService);
  readonly #language = inject(LanguageService);

  readonly permissions = PERMISSIONS;

  /** Bound from the route parameter by `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  readonly customer = signal<CustomerDetail | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);

  /** Tabs are declared here so a new story can append one without touching the template. */
  readonly tabs = [
    { key: 'contacts', labelKey: 'customers.tabs.contacts' },
    { key: 'history', labelKey: 'customers.tabs.history' },
    { key: 'notes', labelKey: 'customers.tabs.notes' },
    { key: 'tickets', labelKey: 'customers.tabs.tickets' },
  ];

  readonly activeTab = signal<string>('contacts');

  readonly locale = computed(() => this.#language.locale());

  readonly displayName = computed(() => {
    const c = this.customer();
    return c ? this.#language.pick({ en: c.displayNameEn, ar: c.displayNameAr }) : '';
  });

  /** Contacts grouped by type so the panel reads as Email, Mobile, Address rather than a flat list. */
  readonly contactGroups = computed(() => {
    const contacts = this.customer()?.contacts ?? [];
    const groups = new Map<ContactType, CustomerContact[]>();

    for (const contact of contacts) {
      const bucket = groups.get(contact.type) ?? [];
      bucket.push(contact);
      groups.set(contact.type, bucket);
    }

    return [...groups.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([type, items]) => ({
        type,
        labelKey: `enums.contactType.${type}`,
        items: items.sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary)),
      }));
  });

  constructor() {
    // `input.required` is resolved before the constructor body runs for routed inputs, so the
    // load is triggered from an effect-free read in ngOnInit-equivalent position.
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.loading.set(true);

    this.#service.getById(this.id()).subscribe({
      next: (customer) => {
        this.customer.set(customer);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  selectTab(key: string): void {
    this.activeTab.set(key);
  }

  statusChipClasses(): string {
    const c = this.customer();
    if (!c) return '';
    if (c.isBlocked) return 'border-rose-200 bg-rose-50 text-rose-700';
    return c.isActive
      ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
      : 'border-slate-200 bg-slate-100 text-slate-600';
  }

  statusLabelKey(): string {
    const c = this.customer();
    if (!c) return '';
    if (c.isBlocked) return 'customers.status.blocked';
    return c.isActive ? 'customers.status.active' : 'customers.status.inactive';
  }
}
