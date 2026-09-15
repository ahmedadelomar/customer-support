import { Component, computed, inject, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import type { Observable } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';
import type { ApiProblem } from '../../../../../core/models/api.models';
import { ChannelKey, CustomerType } from '../../../../../core/models/enums';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { CustomersService } from '../../data-access/customers.service';
import type { CreateCustomerRequest, UpdateCustomerRequest } from '../../data-access/interfaces/customer.interface';

/**
 * Create and edit form for a customer profile. One component serves both, because the fields are
 * identical apart from the contact inputs, which only exist on create.
 *
 * Validation mirrors `CreateCustomerCommandValidator` on the server. The client copy is for
 * responsiveness; the server remains the authority, and its per-field errors are merged back
 * into the form in `#applyServerErrors`.
 */
@Component({
  selector: 'app-customer-form',
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './customer-form.page.html',
})
export class CustomerFormPage {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(CustomersService);
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #toast = inject(ToastService);

  readonly customerId = signal<string | null>(null);
  readonly isEdit = computed(() => this.customerId() !== null);
  readonly saving = signal(false);
  readonly loading = signal(false);

  readonly customerTypes = [
    { value: CustomerType.Individual, labelKey: 'enums.customerType.0' },
    { value: CustomerType.Company, labelKey: 'enums.customerType.1' },
    { value: CustomerType.Government, labelKey: 'enums.customerType.2' },
  ];

  readonly channels = [
    { value: ChannelKey.Email, labelKey: 'enums.channel.0' },
    { value: ChannelKey.WhatsApp, labelKey: 'enums.channel.1' },
    { value: ChannelKey.Sms, labelKey: 'enums.channel.3' },
    { value: ChannelKey.Phone, labelKey: 'enums.channel.6' },
    { value: ChannelKey.Portal, labelKey: 'enums.channel.5' },
  ];

  readonly form = this.#fb.nonNullable.group(
    {
      type: [CustomerType.Individual, Validators.required],
      displayNameEn: ['', [Validators.required, Validators.maxLength(200)]],
      displayNameAr: ['', [Validators.required, Validators.maxLength(200)]],
      firstName: [''],
      lastName: [''],
      companyName: [''],
      nationalIdOrCr: [''],
      taxNumber: [''],
      email: ['', Validators.email],
      phone: ['', Validators.pattern(/^\+?[0-9]{7,15}$/)],
      preferredLanguage: ['ar' as 'ar' | 'en', Validators.required],
      preferredChannel: [ChannelKey.Email, Validators.required],
      tier: [''],
      isActive: [true],
      isBlocked: [false],
      blockedReason: [''],
    },
    { validators: [contactRequired, companyNameRequired, blockedReasonRequired] },
  );

  constructor() {
    const id = this.#route.snapshot.paramMap.get('id');
    if (id) {
      this.customerId.set(id);
      this.#loadForEdit(id);
    }
  }

  /** True when the control should show its error, i.e. it is invalid and the user has engaged with it. */
  showError(name: string): boolean {
    const control = this.form.get(name);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  submit(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();

    // `update` yields void and `create` yields the new id, so widen to a common type before
    // subscribing — otherwise the union of the two call signatures is not callable.
    const request$: Observable<string | void> = this.isEdit()
      ? this.#service.update({
          id: this.customerId()!,
          type: value.type,
          displayNameEn: value.displayNameEn,
          displayNameAr: value.displayNameAr,
          firstName: value.firstName || undefined,
          lastName: value.lastName || undefined,
          companyName: value.companyName || undefined,
          nationalIdOrCr: value.nationalIdOrCr || undefined,
          taxNumber: value.taxNumber || undefined,
          preferredLanguage: value.preferredLanguage,
          preferredChannel: value.preferredChannel,
          tier: value.tier || undefined,
          isActive: value.isActive,
          isBlocked: value.isBlocked,
          blockedReason: value.isBlocked ? value.blockedReason : undefined,
        } satisfies UpdateCustomerRequest)
      : this.#service.create({
          type: value.type,
          displayNameEn: value.displayNameEn,
          displayNameAr: value.displayNameAr,
          firstName: value.firstName || undefined,
          lastName: value.lastName || undefined,
          companyName: value.companyName || undefined,
          nationalIdOrCr: value.nationalIdOrCr || undefined,
          taxNumber: value.taxNumber || undefined,
          email: value.email || undefined,
          phone: value.phone || undefined,
          preferredLanguage: value.preferredLanguage,
          preferredChannel: value.preferredChannel,
          tier: value.tier || undefined,
        } satisfies CreateCustomerRequest);

    request$.subscribe({
      next: (result: string | void) => {
        this.saving.set(false);
        this.#toast.success(this.isEdit() ? 'customers.updated' : 'customers.created');

        const id = this.isEdit() ? this.customerId()! : (result as string);
        void this.#router.navigate(['/agent/customers', id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.#applyServerErrors(error);
      },
    });
  }

  cancel(): void {
    void this.#router.navigate(['/agent/customers']);
  }

  #loadForEdit(id: string): void {
    this.loading.set(true);

    this.#service.getById(id).subscribe({
      next: (customer) => {
        this.form.patchValue({
          type: customer.type,
          displayNameEn: customer.displayNameEn,
          displayNameAr: customer.displayNameAr,
          firstName: customer.firstName ?? '',
          lastName: customer.lastName ?? '',
          companyName: customer.companyName ?? '',
          nationalIdOrCr: customer.nationalIdOrCr ?? '',
          taxNumber: customer.taxNumber ?? '',
          email: customer.primaryEmail ?? '',
          phone: customer.primaryPhone ?? '',
          preferredLanguage: customer.preferredLanguage,
          preferredChannel: customer.preferredChannel,
          tier: customer.tier ?? '',
          isActive: customer.isActive,
          isBlocked: customer.isBlocked,
          blockedReason: customer.blockedReason ?? '',
        });

        // Contacts are managed on the detail page, so they are read-only here.
        this.form.get('email')?.disable();
        this.form.get('phone')?.disable();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        void this.#router.navigate(['/agent/customers']);
      },
    });
  }

  /**
   * Maps a 400 problem-details `errors` map onto the matching controls, so server-only rules
   * (such as duplicate-contact detection) surface next to the field rather than as a bare toast.
   */
  #applyServerErrors(error: unknown): void {
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
      return;
    }

    const problem = error.error as ApiProblem | undefined;
    if (!problem?.errors) {
      return;
    }

    for (const [field, messages] of Object.entries(problem.errors)) {
      // Server property names are PascalCase; controls are camelCase.
      const controlName = field.charAt(0).toLowerCase() + field.slice(1);
      const control = this.form.get(controlName);
      control?.setErrors({ server: messages.join(' ') });
      control?.markAsTouched();
    }
  }
}

/** At least one of email or phone must be present, matching the server rule. */
function contactRequired(group: AbstractControl): ValidationErrors | null {
  const email = group.get('email')?.value;
  const phone = group.get('phone')?.value;

  // Both controls are disabled in edit mode, where contacts are managed elsewhere.
  if (group.get('email')?.disabled) {
    return null;
  }

  return email || phone ? null : { contactRequired: true };
}

/** Company and government customers must carry a company name. */
function companyNameRequired(group: AbstractControl): ValidationErrors | null {
  const type = group.get('type')?.value as CustomerType;
  const companyName = group.get('companyName')?.value;

  const needsCompany = type === CustomerType.Company || type === CustomerType.Government;
  return needsCompany && !companyName ? { companyNameRequired: true } : null;
}

/** Blocking a customer requires a reason, so the audit trail explains the decision. */
function blockedReasonRequired(group: AbstractControl): ValidationErrors | null {
  const isBlocked = group.get('isBlocked')?.value;
  const reason = group.get('blockedReason')?.value;

  return isBlocked && !reason ? { blockedReasonRequired: true } : null;
}
