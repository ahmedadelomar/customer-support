import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import type { Department } from '../../../organization/data-access/organization.service';
import { OrganizationService } from '../../../organization/data-access/organization.service';
import { TicketCategoriesService } from '../../../categories/data-access/ticket-categories.service';
import type { TicketCategoryAdmin } from '../../../categories/data-access/interfaces/ticket-category.interface';
import { TicketPrioritiesService } from '../../../priorities/data-access/ticket-priorities.service';
import type { TicketPriorityAdmin } from '../../../priorities/data-access/interfaces/ticket-priority.interface';
import { DynamicFormComponent } from '../../../../public/web-forms/ui/dynamic-form/dynamic-form.component';
import { WebFormsService } from '../../data-access/web-forms.service';
import type { WebFormDefinitionRequest, WebFormField, WebFormFieldType } from '../../data-access/interfaces/web-form-admin.interface';

/** `mapTo` values the backend understands. `null` means "append to the description". */
const MAP_TO_OPTIONS = ['', 'customerEmail', 'customerPhone', 'customerName', 'subject', 'description', 'categoryCode', 'priorityCode'];
const FIELD_TYPES: WebFormFieldType[] = ['text', 'email', 'phone', 'number', 'date', 'select', 'multiselect', 'textarea', 'checkbox'];

function emptyField(): WebFormField {
  return {
    key: '', type: 'text', labelEn: '', labelAr: '', placeholderEn: null, placeholderAr: null,
    required: false, maxLength: null, pattern: null, options: null, mapTo: null,
  };
}

/** Create/edit a web form, with a field-list builder and a live preview using the same renderer the public page uses. */
@Component({
  selector: 'app-web-form-form',
  imports: [FormsModule, RouterLink, TranslatePipe, PageHeaderComponent, DynamicFormComponent],
  templateUrl: './web-form-form.page.html',
})
export class WebFormFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #service = inject(WebFormsService);
  readonly #categories = inject(TicketCategoriesService);
  readonly #priorities = inject(TicketPrioritiesService);
  readonly #organization = inject(OrganizationService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly FIELD_TYPES = FIELD_TYPES;
  readonly MAP_TO_OPTIONS = MAP_TO_OPTIONS;

  readonly formId = this.#route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.formId;

  readonly loading = signal(this.isEdit);
  readonly saving = signal(false);

  readonly key = signal('');
  readonly titleEn = signal('');
  readonly titleAr = signal('');
  readonly descriptionEn = signal('');
  readonly descriptionAr = signal('');
  readonly submitButtonLabelEn = signal('Send');
  readonly submitButtonLabelAr = signal('إرسال');
  readonly thankYouMessageEn = signal('');
  readonly thankYouMessageAr = signal('');
  readonly fields = signal<WebFormField[]>([]);
  readonly defaultCategoryId = signal('');
  readonly defaultPriorityId = signal('');
  readonly defaultDepartmentId = signal('');
  readonly requireCaptcha = signal(true);
  readonly rateLimitPerHour = signal(10);
  readonly isActive = signal(true);

  readonly categories = signal<TicketCategoryAdmin[]>([]);
  readonly priorities = signal<TicketPriorityAdmin[]>([]);
  readonly departments = signal<Department[]>([]);

  constructor() {
    this.#categories.list().subscribe((categories) => this.categories.set(categories));
    this.#priorities.list().subscribe((priorities) => this.priorities.set(priorities));
    this.#organization.departments().subscribe((departments) => this.departments.set(departments));

    if (this.formId) {
      this.#service.list().subscribe({
        next: (forms) => {
          const form = forms.find((f) => f.id === this.formId);
          if (!form) {
            this.#toast.error('common.notFound');
            void this.#router.navigate(['/admin/web-forms']);
            return;
          }

          this.key.set(form.key);
          this.titleEn.set(form.titleEn);
          this.titleAr.set(form.titleAr);
          this.descriptionEn.set(form.descriptionEn);
          this.descriptionAr.set(form.descriptionAr);
          this.submitButtonLabelEn.set(form.submitButtonLabelEn);
          this.submitButtonLabelAr.set(form.submitButtonLabelAr);
          this.thankYouMessageEn.set(form.thankYouMessageEn);
          this.thankYouMessageAr.set(form.thankYouMessageAr);
          this.fields.set(form.fields);
          this.defaultCategoryId.set(form.defaultCategoryId ?? '');
          this.defaultPriorityId.set(form.defaultPriorityId ?? '');
          this.defaultDepartmentId.set(form.defaultDepartmentId ?? '');
          this.requireCaptcha.set(form.requireCaptcha);
          this.rateLimitPerHour.set(form.rateLimitPerHour);
          this.isActive.set(form.isActive);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  categoryName(category: TicketCategoryAdmin): string {
    return this.#language.pick({ en: category.nameEn, ar: category.nameAr });
  }

  priorityName(priority: TicketPriorityAdmin): string {
    return this.#language.pick({ en: priority.nameEn, ar: priority.nameAr });
  }

  departmentName(department: Department): string {
    return this.#language.pick({ en: department.nameEn, ar: department.nameAr });
  }

  addField(): void {
    this.fields.update((list) => [...list, emptyField()]);
  }

  removeField(index: number): void {
    this.fields.update((list) => list.filter((_, i) => i !== index));
  }

  moveField(index: number, delta: number): void {
    this.fields.update((list) => {
      const target = index + delta;
      if (target < 0 || target >= list.length) return list;
      const copy = [...list];
      [copy[index], copy[target]] = [copy[target], copy[index]];
      return copy;
    });
  }

  updateField(index: number, patch: Partial<WebFormField>): void {
    this.fields.update((list) => list.map((f, i) => (i === index ? { ...f, ...patch } : f)));
  }

  addOption(index: number): void {
    this.updateField(index, {
      options: [...(this.fields()[index].options ?? []), { value: '', labelEn: '', labelAr: '' }],
    });
  }

  removeOption(index: number, optionIndex: number): void {
    const options = (this.fields()[index].options ?? []).filter((_, i) => i !== optionIndex);
    this.updateField(index, { options });
  }

  updateOption(index: number, optionIndex: number, patch: Partial<{ value: string; labelEn: string; labelAr: string }>): void {
    const options = (this.fields()[index].options ?? []).map((o, i) => (i === optionIndex ? { ...o, ...patch } : o));
    this.updateField(index, { options });
  }

  save(): void {
    if (!this.key().trim() || !this.titleEn().trim() || !this.titleAr().trim() || this.fields().length === 0) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const request: WebFormDefinitionRequest = {
      key: this.key().trim(),
      titleEn: this.titleEn(), titleAr: this.titleAr(),
      descriptionEn: this.descriptionEn(), descriptionAr: this.descriptionAr(),
      submitButtonLabelEn: this.submitButtonLabelEn(), submitButtonLabelAr: this.submitButtonLabelAr(),
      thankYouMessageEn: this.thankYouMessageEn(), thankYouMessageAr: this.thankYouMessageAr(),
      fields: this.fields(),
      defaultCategoryId: this.defaultCategoryId() || null,
      defaultPriorityId: this.defaultPriorityId() || null,
      defaultDepartmentId: this.defaultDepartmentId() || null,
      requireCaptcha: this.requireCaptcha(),
      rateLimitPerHour: this.rateLimitPerHour(),
      isActive: this.isActive(),
    };

    const request$: Observable<string | void> = this.formId
      ? this.#service.update(this.formId, request)
      : this.#service.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.formId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/web-forms']);
      },
      error: () => this.saving.set(false),
    });
  }
}
