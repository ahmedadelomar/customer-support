import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HasPermissionDirective } from '../../../../../../../core/directives/has-permission.directive';
import { ContactType } from '../../../../../../../core/models/enums';
import { PERMISSIONS } from '../../../../../../../core/permissions';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { CustomerContactsService } from '../../../../data-access/customer-contacts.service';
import type { CustomerContact } from '../../../../data-access/interfaces/contact.interface';
import { ContactFormDialogComponent } from '../contact-form-dialog/contact-form-dialog.component';
import { ContactVerifyDialogComponent } from '../contact-verify-dialog/contact-verify-dialog.component';

/** Every contact type gets its own section — including ones with no contacts yet — so "Add" is
 *  always reachable rather than only appearing once a first contact of that type already exists. */
const ALL_CONTACT_TYPES: ContactType[] = [
  ContactType.Email,
  ContactType.Mobile,
  ContactType.Phone,
  ContactType.WhatsApp,
  ContactType.Address,
  ContactType.Website,
  ContactType.Other,
];

/** Types a code can actually be delivered to today (see `SendContactVerificationCommand`). */
const VERIFIABLE_TYPES = new Set([ContactType.Email, ContactType.Mobile]);

/**
 * The editable Contact details tab. Replaces the read-only listing that shipped with CS-101 — see
 * `.squad/plans/customer-management/11-story-contact-details-CS-102-contact-details.md`.
 */
@Component({
  selector: 'app-contacts-panel',
  imports: [TranslatePipe, HasPermissionDirective, ContactFormDialogComponent, ContactVerifyDialogComponent],
  templateUrl: './contacts-panel.component.html',
})
export class ContactsPanelComponent implements OnChanges {
  readonly #service = inject(CustomerContactsService);
  readonly #toast = inject(ToastService);
  readonly #translate = inject(TranslateService);

  readonly customerId = input.required<string>();

  /** Lets the parent refresh the profile header (denormalised primary email/phone) after a change. */
  readonly changed = output<void>();

  readonly permissions = PERMISSIONS;
  readonly contactTypes = ALL_CONTACT_TYPES;
  readonly ContactType = ContactType;

  readonly contacts = signal<CustomerContact[]>([]);
  readonly loading = signal(true);

  readonly formOpen = signal(false);
  readonly formType = signal<ContactType>(ContactType.Email);
  readonly formContact = signal<CustomerContact | null>(null);

  readonly verifyOpen = signal(false);
  readonly verifyContact = signal<CustomerContact | null>(null);

  readonly groups = computed(() => {
    const byType = new Map<ContactType, CustomerContact[]>();
    for (const contact of this.contacts()) {
      const bucket = byType.get(contact.type) ?? [];
      bucket.push(contact);
      byType.set(contact.type, bucket);
    }

    return this.contactTypes.map((type) => ({
      type,
      labelKey: `enums.contactType.${type}`,
      isVerifiable: VERIFIABLE_TYPES.has(type),
      items: (byType.get(type) ?? []).sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary)),
    }));
  });

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list(this.customerId()).subscribe({
      next: (contacts) => {
        this.contacts.set(contacts);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openAdd(type: ContactType): void {
    this.formType.set(type);
    this.formContact.set(null);
    this.formOpen.set(true);
  }

  openEdit(contact: CustomerContact): void {
    this.formType.set(contact.type);
    this.formContact.set(contact);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  onFormSaved(): void {
    this.formOpen.set(false);
    this.load();
    this.changed.emit();
  }

  setPrimary(contact: CustomerContact): void {
    this.#service.setPrimary(this.customerId(), contact.id).subscribe({
      next: () => {
        this.#toast.success('customers.contacts.primarySet');
        this.load();
        this.changed.emit();
      },
    });
  }

  deleteContact(contact: CustomerContact): void {
    const label = contact.label ? `${contact.value} (${contact.label})` : contact.value;
    if (!confirm(this.#translate.instant('customers.contacts.deleteConfirm', { value: label }))) {
      return;
    }

    this.#service.delete(this.customerId(), contact.id).subscribe({
      next: () => {
        this.#toast.success('customers.contacts.deleted');
        this.load();
        this.changed.emit();
      },
    });
  }

  openVerify(contact: CustomerContact): void {
    this.verifyContact.set(contact);
    this.verifyOpen.set(true);
  }

  closeVerify(): void {
    this.verifyOpen.set(false);
  }

  onVerified(): void {
    this.load();
  }

  /** City, postal code and country joined for display, skipping whichever parts are blank. */
  addressSummary(contact: CustomerContact): string {
    return [contact.city, contact.postalCode, contact.countryCode].filter(Boolean).join(', ');
  }
}
