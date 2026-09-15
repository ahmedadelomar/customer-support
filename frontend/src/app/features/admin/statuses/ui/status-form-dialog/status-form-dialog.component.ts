import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { TicketStatusKind } from '../../../../../core/models/enums';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TicketStatusesService } from '../../data-access/ticket-statuses.service';
import type { TicketStatusAdmin } from '../../data-access/interfaces/ticket-status.interface';

const KIND_OPTIONS: { value: TicketStatusKind; labelKey: string }[] = [
  { value: TicketStatusKind.New, labelKey: 'enums.ticketStatusKind.0' },
  { value: TicketStatusKind.Open, labelKey: 'enums.ticketStatusKind.1' },
  { value: TicketStatusKind.Pending, labelKey: 'enums.ticketStatusKind.2' },
  { value: TicketStatusKind.OnHold, labelKey: 'enums.ticketStatusKind.3' },
  { value: TicketStatusKind.Resolved, labelKey: 'enums.ticketStatusKind.4' },
  { value: TicketStatusKind.Closed, labelKey: 'enums.ticketStatusKind.5' },
  { value: TicketStatusKind.Cancelled, labelKey: 'enums.ticketStatusKind.6' },
];

/**
 * Create or edit a status. Changing `Kind` while tickets sit in this status is refused by the server
 * unless resubmitted with `force: true` — this dialog surfaces that as a loud, explicit confirm step
 * rather than a generic error, per the story's "warn loudly" requirement.
 */
@Component({
  selector: 'app-status-form-dialog',
  imports: [ReactiveFormsModule, TranslatePipe, ModalComponent],
  templateUrl: './status-form-dialog.component.html',
})
export class StatusFormDialogComponent implements OnChanges {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(TicketStatusesService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly status = input<TicketStatusAdmin | null>(null);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly saving = signal(false);
  readonly isEdit = computed(() => this.status() !== null);
  readonly kindOptions = KIND_OPTIONS;

  /** Set once the server refuses a Kind change with a ticket count; resubmitting sets `force`. */
  readonly kindChangeWarning = signal<number | null>(null);

  readonly form = this.#fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(64)]],
    nameEn: ['', [Validators.required, Validators.maxLength(200)]],
    nameAr: ['', [Validators.required, Validators.maxLength(200)]],
    kind: [TicketStatusKind.Open, Validators.required],
    colorHex: ['#64748B', [Validators.required, Validators.pattern(/^#[0-9A-Fa-f]{6}$/)]],
    isTerminal: [false],
    pausesSla: [false],
    isDefault: [false],
    isVisibleInPortal: [true],
    isActive: [true],
  });

  ngOnChanges(): void {
    const s = this.status();
    this.kindChangeWarning.set(null);

    this.form.reset({
      code: s?.code ?? '',
      nameEn: s?.nameEn ?? '',
      nameAr: s?.nameAr ?? '',
      kind: s?.kind ?? TicketStatusKind.Open,
      colorHex: s?.colorHex ?? '#64748B',
      isTerminal: s?.isTerminal ?? false,
      pausesSla: s?.pausesSla ?? false,
      isDefault: s?.isDefault ?? false,
      isVisibleInPortal: s?.isVisibleInPortal ?? true,
      isActive: s?.isActive ?? true,
    });

    if (s) {
      this.form.get('code')?.disable();
    } else {
      this.form.get('code')?.enable();
    }
  }

  showError(name: string): boolean {
    const control = this.form.get(name);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  submit(force = false): void {
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    this.kindChangeWarning.set(null);
    const value = this.form.getRawValue();
    const existing = this.status();

    const request$: Observable<string | void> = existing
      ? this.#service.update({
          id: existing.id,
          nameEn: value.nameEn,
          nameAr: value.nameAr,
          kind: value.kind,
          colorHex: value.colorHex,
          isTerminal: value.isTerminal,
          pausesSla: value.pausesSla,
          isDefault: value.isDefault,
          isVisibleInPortal: value.isVisibleInPortal,
          isActive: value.isActive,
          force,
        })
      : this.#service.create({
          code: value.code,
          nameEn: value.nameEn,
          nameAr: value.nameAr,
          kind: value.kind,
          colorHex: value.colorHex,
          isTerminal: value.isTerminal,
          pausesSla: value.pausesSla,
          isDefault: value.isDefault,
          isVisibleInPortal: value.isVisibleInPortal,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'admin.statuses.updated' : 'admin.statuses.created');
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        const ticketCount = this.#extractTicketCount(error);
        if (ticketCount !== null) {
          this.kindChangeWarning.set(ticketCount);
        }
      },
    });
  }

  confirmKindChange(): void {
    this.submit(true);
  }

  cancelKindChange(): void {
    this.kindChangeWarning.set(null);
  }

  close(): void {
    this.closed.emit();
  }

  #extractTicketCount(error: unknown): number | null {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { ticketCount?: number } }).error;
      if (typeof problem?.ticketCount === 'number') return problem.ticketCount;
    }
    return null;
  }
}
