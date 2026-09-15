import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../../core/auth/auth.service';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { StateCardComponent } from '../../../../../shared/ui/state-card/state-card.component';
import { CustomersService } from '../../data-access/customers.service';
import type { CustomerDetail } from '../../data-access/interfaces/customer.interface';
import { ContactsPanelComponent } from './ui/contacts-panel/contacts-panel.component';
import { InteractionTimelineComponent } from './ui/interaction-timeline/interaction-timeline.component';
import { NotesPanelComponent } from './ui/notes-panel/notes-panel.component';

/**
 * Customer profile page. The Contact details tab is `ContactsPanelComponent` (CS-102), the History
 * tab is `InteractionTimelineComponent` (CS-103), and the Notes tab is `NotesPanelComponent`
 * (CS-104); the tickets tab is added by its own story against this same shell.
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
    ContactsPanelComponent,
    InteractionTimelineComponent,
    NotesPanelComponent,
  ],
  templateUrl: './customer-detail.page.html',
})
export class CustomerDetailPage {
  readonly #service = inject(CustomersService);
  readonly #language = inject(LanguageService);
  readonly #auth = inject(AuthService);

  readonly permissions = PERMISSIONS;
  readonly canViewHistory = computed(() => this.#auth.hasPermission(PERMISSIONS.customers.viewHistory));
  readonly canViewNotes = computed(() => this.#auth.hasPermission(PERMISSIONS.customers.viewNotes));

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

  /**
   * Re-fetches the profile without the loading skeleton, so a contact change (which manages its
   * own panel-level loading state) doesn't blank the whole page while it refreshes the header's
   * denormalised primary email/phone.
   */
  refreshHeader(): void {
    this.#service.getById(this.id()).subscribe({ next: (customer) => this.customer.set(customer) });
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
