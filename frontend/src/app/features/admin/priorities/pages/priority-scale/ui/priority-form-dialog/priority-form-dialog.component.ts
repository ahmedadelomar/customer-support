import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../../../shared/ui/modal/modal.component';
import { TicketPrioritiesService } from '../../../../data-access/ticket-priorities.service';
import type { TicketPriorityAdmin } from '../../../../data-access/interfaces/ticket-priority.interface';
import type { Observable } from 'rxjs';

/** Create or edit a priority. Level (position in the scale) changes only through drag/reorder on the list, not here. */
@Component({
  selector: 'app-priority-form-dialog',
  imports: [ReactiveFormsModule, TranslatePipe, ModalComponent],
  templateUrl: './priority-form-dialog.component.html',
})
export class PriorityFormDialogComponent implements OnChanges {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(TicketPrioritiesService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  readonly priority = input<TicketPriorityAdmin | null>(null);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly saving = signal(false);
  readonly isEdit = computed(() => this.priority() !== null);

  readonly form = this.#fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(64)]],
    nameEn: ['', [Validators.required, Validators.maxLength(200)]],
    nameAr: ['', [Validators.required, Validators.maxLength(200)]],
    colorHex: ['#64748B', [Validators.required, Validators.pattern(/^#[0-9A-Fa-f]{6}$/)]],
    isDefault: [false],
    isActive: [true],
  });

  ngOnChanges(): void {
    const p = this.priority();

    this.form.reset({
      code: p?.code ?? '',
      nameEn: p?.nameEn ?? '',
      nameAr: p?.nameAr ?? '',
      colorHex: p?.colorHex ?? '#64748B',
      isDefault: p?.isDefault ?? false,
      isActive: p?.isActive ?? true,
    });

    // The code is immutable once created; disable rather than hide, so its value is still visible.
    if (p) {
      this.form.get('code')?.disable();
    } else {
      this.form.get('code')?.enable();
    }
  }

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
    const existing = this.priority();

    const request$: Observable<string | void> = existing
      ? this.#service.update({
          id: existing.id,
          nameEn: value.nameEn,
          nameAr: value.nameAr,
          colorHex: value.colorHex,
          isDefault: value.isDefault,
          isActive: value.isActive,
        })
      : this.#service.create({
          code: value.code,
          nameEn: value.nameEn,
          nameAr: value.nameAr,
          colorHex: value.colorHex,
          isDefault: value.isDefault,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'admin.priorities.updated' : 'admin.priorities.created');
        this.saved.emit();
      },
      error: () => this.saving.set(false),
    });
  }

  close(): void {
    this.closed.emit();
  }
}
