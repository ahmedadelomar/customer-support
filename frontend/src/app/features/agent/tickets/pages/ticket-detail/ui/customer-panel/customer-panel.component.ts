import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ChannelKey, ContactType } from '../../../../../../../core/models/enums';
import { LanguageService } from '../../../../../../../core/services/language.service';
import { relativeTime } from '../../../../../../../shared/utils/relative-time';
import { CustomersService } from '../../../../../customers/data-access/customers.service';
import type { CustomerContact } from '../../../../../customers/data-access/interfaces/contact.interface';
import type { InlinePatchCustomerRequest } from '../../../../../customers/data-access/interfaces/customer.interface';
import type { CustomerPanel } from '../../../../data-access/interfaces/ticket.interface';

type EditableField = 'displayNameEn' | 'displayNameAr' | 'preferredLanguage' | 'preferredChannel' | 'tier';

/**
 * Left column of the ticket detail screen (Agent Dashboard / Customer information, CS-402): identity,
 * tier, blocked state, contacts, other open tickets, pinned notes and a satisfaction/last-interaction
 * footer — all delivered with the ticket in `TicketDetailDto.Customer`, never a second request. Below
 * `lg` this collapses to a disclosure above the conversation (see `collapsed` and the
 * `lg:!block` override in the template), expanded by default so context is never hidden on first open.
 */
@Component({
  selector: 'app-customer-panel',
  imports: [RouterLink, FormsModule, TranslatePipe],
  templateUrl: './customer-panel.component.html',
})
export class CustomerPanelComponent {
  readonly #customersService = inject(CustomersService);
  readonly #language = inject(LanguageService);

  readonly customer = input.required<CustomerPanel>();

  /** Lets the parent re-fetch the ticket so the panel's own `customer()` input reflects a saved edit. */
  readonly edited = output<void>();

  readonly ContactType = ContactType;

  readonly collapsed = signal(false);

  readonly editingField = signal<EditableField | null>(null);
  readonly draftValue = signal('');
  readonly saving = signal(false);

  readonly languageOptions = [
    { value: 'ar', labelKey: 'common.language.ar' },
    { value: 'en', labelKey: 'common.language.en' },
  ];

  readonly channelOptions = [
    { value: ChannelKey.Email, labelKey: 'enums.channel.0' },
    { value: ChannelKey.WhatsApp, labelKey: 'enums.channel.1' },
    { value: ChannelKey.Sms, labelKey: 'enums.channel.3' },
    { value: ChannelKey.Phone, labelKey: 'enums.channel.6' },
    { value: ChannelKey.Portal, labelKey: 'enums.channel.5' },
  ];

  readonly displayName = computed(() => {
    const c = this.customer();
    return this.#language.pick({ en: c.displayNameEn, ar: c.displayNameAr });
  });

  readonly locale = computed(() => this.#language.locale());
  readonly language = computed(() => this.#language.current());

  toggleCollapsed(): void {
    this.collapsed.update((v) => !v);
  }

  channelLabelKey(): string {
    const found = this.channelOptions.find((o) => o.value === this.customer().preferredChannel);
    return found?.labelKey ?? 'enums.channel.0';
  }

  contactHref(contact: CustomerContact): string | null {
    if (contact.type === ContactType.Email) return `mailto:${contact.value}`;
    if (contact.type === ContactType.Mobile || contact.type === ContactType.Phone || contact.type === ContactType.WhatsApp) {
      return `tel:${contact.value}`;
    }
    return null;
  }

  authorName(note: { authorNameEn: string | null; authorNameAr: string | null }): string | undefined {
    if (!note.authorNameEn && !note.authorNameAr) return undefined;
    return this.#language.pick({ en: note.authorNameEn ?? '', ar: note.authorNameAr ?? '' });
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  canEditInline(): boolean {
    return this.customer().canEdit && this.editingField() === null;
  }

  isEditing(field: EditableField): boolean {
    return this.editingField() === field;
  }

  startEdit(field: EditableField): void {
    if (!this.customer().canEdit || this.saving()) return;
    this.editingField.set(field);
    this.draftValue.set(this.#currentValue(field));
  }

  cancelEdit(): void {
    this.editingField.set(null);
  }

  onFieldKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.saveEdit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelEdit();
    }
  }

  saveEdit(): void {
    const field = this.editingField();
    if (!field) return;

    const c = this.customer();
    const draft = this.draftValue();

    const request: InlinePatchCustomerRequest = {
      id: c.id,
      displayNameEn: field === 'displayNameEn' ? draft : c.displayNameEn,
      displayNameAr: field === 'displayNameAr' ? draft : c.displayNameAr,
      preferredLanguage: field === 'preferredLanguage' ? draft : c.preferredLanguage,
      preferredChannel: field === 'preferredChannel' ? (Number(draft) as ChannelKey) : c.preferredChannel,
      tier: (field === 'tier' ? draft : c.tier) || undefined,
    };

    this.saving.set(true);
    this.#customersService.inlinePatch(request).subscribe({
      // The global HTTP error interceptor already shows a toast on failure; this component just
      // needs to leave editing mode either way — nothing was applied locally, so a failure simply
      // shows the pre-edit value again once `editingField` clears.
      next: () => {
        this.saving.set(false);
        this.editingField.set(null);
        this.edited.emit();
      },
      error: () => {
        this.saving.set(false);
        this.editingField.set(null);
      },
    });
  }

  #currentValue(field: EditableField): string {
    const c = this.customer();
    switch (field) {
      case 'displayNameEn':
        return c.displayNameEn;
      case 'displayNameAr':
        return c.displayNameAr;
      case 'preferredLanguage':
        return c.preferredLanguage;
      case 'preferredChannel':
        return String(c.preferredChannel);
      case 'tier':
        return c.tier ?? '';
    }
  }
}
