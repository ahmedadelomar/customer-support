import { Component, type OnChanges, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { TicketCategoriesService } from '../../data-access/ticket-categories.service';
import type {
  CategoryDefaultOption,
  TicketCategoryAdmin,
} from '../../data-access/interfaces/ticket-category.interface';

/** Create or edit a category node. One component serves both — the fields are identical; only the submit call and the parent differ. */
@Component({
  selector: 'app-category-form-dialog',
  imports: [ReactiveFormsModule, TranslatePipe, ModalComponent],
  templateUrl: './category-form-dialog.component.html',
})
export class CategoryFormDialogComponent implements OnChanges {
  readonly #fb = inject(FormBuilder);
  readonly #service = inject(TicketCategoriesService);
  readonly #toast = inject(ToastService);

  readonly open = input(false);
  /** Null in add mode. */
  readonly category = input<TicketCategoryAdmin | null>(null);
  /** Parent for a new node; ignored when editing. */
  readonly parentId = input<string | null>(null);
  readonly priorityOptions = input<CategoryDefaultOption[]>([]);
  readonly departmentOptions = input<CategoryDefaultOption[]>([]);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly saving = signal(false);

  readonly form = this.#fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(64), Validators.pattern(/^[a-z0-9-]+$/)]],
    nameEn: ['', [Validators.required, Validators.maxLength(200)]],
    nameAr: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    displayOrder: [0],
    defaultPriorityId: [''],
    defaultDepartmentId: [''],
    isVisibleInPortal: [true],
  });

  ngOnChanges(): void {
    const c = this.category();

    this.form.reset({
      code: c?.code ?? '',
      nameEn: c?.nameEn ?? '',
      nameAr: c?.nameAr ?? '',
      description: c?.description ?? '',
      displayOrder: c?.displayOrder ?? 0,
      defaultPriorityId: c?.defaultPriorityId ?? '',
      defaultDepartmentId: c?.defaultDepartmentId ?? '',
      isVisibleInPortal: c?.isVisibleInPortal ?? true,
    });
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

    const payload = {
      code: value.code,
      nameEn: value.nameEn,
      nameAr: value.nameAr,
      description: value.description || undefined,
      displayOrder: value.displayOrder,
      defaultPriorityId: value.defaultPriorityId || undefined,
      defaultDepartmentId: value.defaultDepartmentId || undefined,
      isVisibleInPortal: value.isVisibleInPortal,
    };

    const existing = this.category();
    const request$: Observable<string | void> = existing
      ? this.#service.update({ ...payload, id: existing.id })
      : this.#service.create({ ...payload, parentId: this.parentId() ?? undefined });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'admin.categories.updated' : 'admin.categories.created');
        this.saved.emit();
      },
      error: () => this.saving.set(false),
    });
  }

  close(): void {
    this.closed.emit();
  }
}
