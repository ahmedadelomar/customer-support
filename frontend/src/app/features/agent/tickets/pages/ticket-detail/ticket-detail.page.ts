import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { TicketStatusKind } from '../../../../../core/models/enums';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { TicketsService } from '../../data-access/tickets.service';
import type { TicketLookups } from '../../data-access/interfaces/ticket-lookups.interface';
import type { TicketDetail } from '../../data-access/interfaces/ticket.interface';
import { CustomerPanelComponent } from './ui/customer-panel/customer-panel.component';
import { ConversationThreadComponent } from './ui/conversation-thread/conversation-thread.component';
import { PropertiesPanelComponent } from './ui/properties-panel/properties-panel.component';
import { MergeDialogComponent } from './ui/merge-dialog/merge-dialog.component';
import { AssignDialogComponent } from './ui/assign-dialog/assign-dialog.component';
import { ResolveDialogComponent } from './ui/resolve-dialog/resolve-dialog.component';
import { EscalateDialogComponent } from './ui/escalate-dialog/escalate-dialog.component';
import { HistoryTabComponent } from './ui/history-tab/history-tab.component';

/**
 * Ticket detail screen: customer panel, conversation thread and properties panel — the three-column
 * layout the plan calls for, stacked on mobile. Tabs above the thread are declared here so History
 * (CS-205) and Related can be added without touching this shell.
 */
@Component({
  selector: 'app-ticket-detail',
  imports: [
    RouterLink,
    TranslatePipe,
    CustomerPanelComponent,
    ConversationThreadComponent,
    PropertiesPanelComponent,
    MergeDialogComponent,
    AssignDialogComponent,
    ResolveDialogComponent,
    EscalateDialogComponent,
    HistoryTabComponent,
  ],
  templateUrl: './ticket-detail.page.html',
})
export class TicketDetailPage {
  readonly #service = inject(TicketsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly permissions = PERMISSIONS;

  /** Bound from the route parameter by `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  readonly ticket = signal<TicketDetail | null>(null);
  readonly lookups = signal<TicketLookups | null>(null);
  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly mergeDialogOpen = signal(false);
  readonly assignDialogOpen = signal(false);
  readonly resolveDialogOpen = signal(false);
  readonly resolveStatusId = signal('');
  readonly escalateDialogOpen = signal(false);

  readonly tabs = [
    { key: 'conversation', labelKey: 'tickets.tabs.conversation' },
    { key: 'history', labelKey: 'tickets.tabs.history' },
    { key: 'related', labelKey: 'tickets.tabs.related' },
  ];

  readonly activeTab = signal<string>('conversation');

  readonly displayName = computed(() => {
    const t = this.ticket();
    if (!t) return '';
    return this.#language.pick({ en: t.customer.displayNameEn, ar: t.customer.displayNameAr });
  });

  constructor() {
    // `input.required` resolves before the constructor body runs for routed inputs, so the load
    // is triggered from an effect-free read in ngOnInit-equivalent position.
    queueMicrotask(() => this.load());
    this.#service.lookups().subscribe((lookups) => this.lookups.set(lookups));
  }

  load(): void {
    this.loading.set(true);

    this.#service.getById(this.id()).subscribe({
      next: (ticket) => {
        this.ticket.set(ticket);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  /** Re-fetches after a reply, note or property change updates denormalised header fields. */
  refresh(): void {
    this.#service.getById(this.id()).subscribe({ next: (ticket) => this.ticket.set(ticket) });
  }

  selectTab(key: string): void {
    this.activeTab.set(key);
  }

  openMergeDialog(): void {
    this.mergeDialogOpen.set(true);
  }

  closeMergeDialog(): void {
    this.mergeDialogOpen.set(false);
  }

  onMerged(): void {
    this.mergeDialogOpen.set(false);
    this.load();
  }

  openAssignDialog(): void {
    this.assignDialogOpen.set(true);
  }

  closeAssignDialog(): void {
    this.assignDialogOpen.set(false);
  }

  onAssigned(): void {
    this.assignDialogOpen.set(false);
    this.load();
  }

  openResolveDialog(statusId: string): void {
    this.resolveStatusId.set(statusId);
    this.resolveDialogOpen.set(true);
  }

  closeResolveDialog(): void {
    this.resolveDialogOpen.set(false);
  }

  onResolved(): void {
    this.resolveDialogOpen.set(false);
    this.load();
  }

  openEscalateDialog(): void {
    this.escalateDialogOpen.set(true);
  }

  closeEscalateDialog(): void {
    this.escalateDialogOpen.set(false);
  }

  onEscalated(): void {
    this.escalateDialogOpen.set(false);
    this.load();
  }

  /** Shortcut for the terminal banner: reopens straight into the workflow's Open-kind status, without the full status picker. */
  reopen(): void {
    const openStatus = this.lookups()?.statuses.find((s) => s.kind === TicketStatusKind.Open);
    if (!openStatus) return;

    this.#service.changeStatus(this.id(), { statusId: openStatus.id }).subscribe({
      next: () => {
        this.#toast.success('tickets.properties.statusChanged');
        this.load();
      },
    });
  }
}
