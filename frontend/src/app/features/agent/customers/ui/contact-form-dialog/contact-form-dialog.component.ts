import { HttpErrorResponse } from '@angular/common/http';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { ApiProblem } from '../../../../../core/models/api.models';
import { ContactType } from '../../../../../core/models/enums';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { CustomerContactsService } from '../../data-access/customer-contacts.service';
import type { CustomerContact, DuplicateContactProblem } from '../../data-access/interfaces/contact.interface';

/**
 * Add or edit a single contact. One component serves both because the fields are identical; only
 * the type (fixed on add, immutable on edit — see `UpdateCustomerContactCommand`) and the submit
 * call differ.
 *
 * The duplicate-warning flow lives here rather than as a separate dialog: a 409 from the server
 * with `duplicateCustomerId`/`duplicateCustomerName` switches the same dialog into a confirm step,
 * so the agent's typed values are never lost while they decide.
 */
@Component({
  selector: 'app-contact-form-dialog',
  imports: [ReactiveFormsModule, TranslatePipe, ModalComponent],
  templateUrl: './contact-form-dialog.component.html',
})
export class ContactFormDialogComponent implements OnChanges {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(CustomerContactsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly customerId = input.required<string>();
  /** The type for a new contact, or the existing contact's (immutable) type when editing. */
  readonly type = input.required<ContactType>();
  /** Null in add mode. */
  readonly contact = input<CustomerContact | null>(null);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly ContactType = ContactType;
  readonly isEdit = computed(() => this.contact() !== null);
  readonly isAddressType = computed(() => this.type() === ContactType.Address);
  readonly saving = signal(false);
  readonly duplicateWarning = signal<DuplicateContactProblem | null>(null);

  readonly form = this.#fb.nonNullable.group({
    value: ['', Validators.required],
    label: [''],
    countryCode: [''],
    city: [''],
    addressLine: [''],
    postalCode: [''],
    allowNotifications: [true],
  });

  /**
   * Re-seeds the form whenever an input changes — most importantly when `open` flips to `true`, so
   * a stale value from the previous use (add after edit, or edit of a different contact) never
   * leaks in. Signal inputs still route through `ngOnChanges`, so this needs no manual effect.
   */
  ngOnChanges(): void {
    this.#reset();
  }

  showError(name: string): boolean {
    const control = this.form.get(name);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  submit(confirmDuplicate = false): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    this.duplicateWarning.set(null);
    const value = this.form.getRawValue();

    const payload = {
      value: value.value,
      label: value.label || undefined,
      countryCode: value.countryCode || undefined,
      city: value.city || undefined,
      addressLine: value.addressLine || undefined,
      postalCode: value.postalCode || undefined,
      confirmDuplicate,
    };

    const existing = this.contact();
    const request$ = existing
      ? this.#service.update(this.customerId(), existing.id, {
          ...payload,
          allowNotifications: value.allowNotifications,
        })
      : this.#service.add(this.customerId(), { ...payload, type: this.type() });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'customers.contacts.updated' : 'customers.contacts.added');
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);

        if (error instanceof HttpErrorResponse && error.status === 409) {
          const problem = error.error as (ApiProblem & Partial<DuplicateContactProblem>) | undefined;

          if (problem?.duplicateCustomerId) {
            this.duplicateWarning.set({
              duplicateCustomerId: problem.duplicateCustomerId,
              duplicateCustomerName: problem.duplicateCustomerName!,
            });
            return;
          }
        }

        // Any other failure (validation, not-found) surfaces via the global error interceptor toast.
      },
    });
  }

  confirmDuplicateAndSave(): void {
    this.submit(true);
  }

  cancelDuplicate(): void {
    this.duplicateWarning.set(null);
  }

  close(): void {
    this.closed.emit();
  }

  #reset(): void {
    this.duplicateWarning.set(null);
    const c = this.contact();

    this.form.reset({
      value: c?.value ?? '',
      label: c?.label ?? '',
      countryCode: c?.countryCode ?? '',
      city: c?.city ?? '',
      addressLine: c?.addressLine ?? '',
      postalCode: c?.postalCode ?? '',
      allowNotifications: c?.allowNotifications ?? true,
    });

    this.#applyTypeValidators();
  }

  #applyTypeValidators(): void {
    const valueControl = this.form.get('value')!;
    const addressLineControl = this.form.get('addressLine')!;
    const type = this.type();

    const validators = [Validators.required];
    if (type === ContactType.Email) {
      validators.push(Validators.email);
    } else if (type === ContactType.Mobile || type === ContactType.Phone || type === ContactType.WhatsApp) {
      validators.push(Validators.pattern(/^\+?[0-9]{7,15}$/));
    }

    if (type === ContactType.Address) {
      // The address line is the required field; "value" becomes optional free text (e.g. a nickname).
      valueControl.clearValidators();
      addressLineControl.setValidators([Validators.required]);
    } else {
      valueControl.setValidators(validators);
      addressLineControl.clearValidators();
    }

    valueControl.updateValueAndValidity();
    addressLineControl.updateValueAndValidity();
  }
}
