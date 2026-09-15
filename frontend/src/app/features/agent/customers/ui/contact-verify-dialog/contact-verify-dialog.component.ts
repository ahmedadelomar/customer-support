import { HttpErrorResponse } from '@angular/common/http';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { ApiProblem } from '../../../../../core/models/api.models';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { CustomerContactsService } from '../../data-access/customer-contacts.service';
import type { CustomerContact } from '../../data-access/interfaces/contact.interface';

type Stage = 'idle' | 'sent' | 'verified';

/**
 * Send-then-confirm flow for one contact. Kept as its own dialog rather than folded into the
 * add/edit form because the two are unrelated actions with unrelated lifecycles — editing a value
 * clears its verification, but verifying does not open an edit form.
 */
@Component({
  selector: 'app-contact-verify-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './contact-verify-dialog.component.html',
})
export class ContactVerifyDialogComponent implements OnChanges {
  readonly #service = inject(CustomerContactsService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly customerId = input.required<string>();
  readonly contact = input<CustomerContact | null>(null);

  readonly verified = output<void>();
  readonly closed = output<void>();

  readonly stage = signal<Stage>('idle');
  readonly sending = signal(false);
  readonly confirming = signal(false);
  readonly code = signal('');
  readonly expiresAt = signal<Date | null>(null);
  readonly devCode = signal<string | null>(null);
  readonly remainingAttempts = signal(5);
  readonly errorMessage = signal<string | null>(null);

  /** Countdown resend cooldown, independent of and shorter than the server's per-hour cap. */
  readonly resendCooldownUntil = signal<number>(0);
  readonly canResend = computed(() => Date.now() >= this.resendCooldownUntil());

  ngOnChanges(): void {
    this.stage.set('idle');
    this.sending.set(false);
    this.confirming.set(false);
    this.code.set('');
    this.expiresAt.set(null);
    this.devCode.set(null);
    this.remainingAttempts.set(5);
    this.errorMessage.set(null);
  }

  send(): void {
    const contact = this.contact();
    if (!contact) return;

    this.sending.set(true);
    this.errorMessage.set(null);

    this.#service.sendVerification(this.customerId(), contact.id).subscribe({
      next: (result) => {
        this.sending.set(false);
        this.stage.set('sent');
        this.expiresAt.set(new Date(result.expiresAt));
        this.devCode.set(result.devCode ?? null);
        this.remainingAttempts.set(5);
        this.resendCooldownUntil.set(Date.now() + 60_000);
      },
      error: (error: unknown) => {
        this.sending.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  confirm(): void {
    const contact = this.contact();
    if (!contact || this.code().length !== 6) return;

    this.confirming.set(true);
    this.errorMessage.set(null);

    this.#service.confirmVerification(this.customerId(), contact.id, this.code()).subscribe({
      next: (result) => {
        this.confirming.set(false);

        if (result.success) {
          this.stage.set('verified');
          this.#toast.success('customers.contacts.verifyDialog.verified');
          this.verified.emit();
        } else {
          this.remainingAttempts.set(result.remainingAttempts);
          this.code.set('');
          this.errorMessage.set(null);
        }
      },
      error: (error: unknown) => {
        this.confirming.set(false);
        // Expired or attempts exhausted — nothing left to retry against; the agent must resend.
        this.errorMessage.set(this.#extractMessage(error));
        this.stage.set('idle');
      },
    });
  }

  close(): void {
    this.closed.emit();
  }

  /**
   * Returns the server's own detail text verbatim (matching how `customer-form.page` shows
   * server-side validation messages), or `null` to let the template fall back to a translated
   * generic message.
   */
  #extractMessage(error: unknown): string | null {
    if (error instanceof HttpErrorResponse) {
      const problem = error.error as ApiProblem | undefined;
      if (problem?.detail) return problem.detail;
    }
    return null;
  }
}
