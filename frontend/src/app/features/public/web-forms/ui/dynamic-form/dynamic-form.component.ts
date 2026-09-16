import { Component, effect, inject, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import type { WebFormField } from '../../data-access/interfaces/web-form.interface';

/**
 * Builds a reactive form straight from a form's field schema (Communication Channels / Web forms,
 * CS-305) — one control per field, validators derived from `required`/`maxLength`/`pattern`, so a
 * new field needs no new component code. Used both by the public submission page and by the admin
 * builder's live preview, which is what keeps "what the administrator sees" and "what visitors get"
 * from ever drifting apart.
 */
@Component({
  selector: 'app-dynamic-form',
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './dynamic-form.component.html',
})
export class DynamicFormComponent {
  readonly #language = inject(LanguageService);

  readonly fields = input.required<WebFormField[]>();
  readonly submitLabel = input<string>('');
  readonly submitting = input(false);

  readonly submitted = output<Record<string, string>>();

  form = new FormGroup({});

  constructor() {
    effect(() => {
      this.form = this.#buildForm(this.fields());
    });
  }

  label(field: WebFormField): string {
    return this.#language.pick({ en: field.labelEn, ar: field.labelAr });
  }

  placeholder(field: WebFormField): string {
    return this.#language.pick({ en: field.placeholderEn ?? '', ar: field.placeholderAr ?? '' });
  }

  optionLabel(option: { labelEn: string; labelAr: string }): string {
    return this.#language.pick({ en: option.labelEn, ar: option.labelAr });
  }

  control(key: string): FormControl {
    return this.form.get(key) as FormControl;
  }

  hasError(field: WebFormField): boolean {
    const control = this.control(field.key);
    return control.invalid && (control.dirty || control.touched);
  }

  errorMessage(field: WebFormField): string | null {
    const errors = this.control(field.key).errors;
    if (!errors) return null;
    if (errors['server']) return errors['server'] as string;
    if (errors['required']) return null; // rendered via a translated key in the template
    return null;
  }

  /** Called by the parent after a 400 — server-only rules surface next to the field, not as a bare toast. */
  setServerErrors(errors: Record<string, string[]>): void {
    for (const [key, messages] of Object.entries(errors)) {
      const control = this.form.get(key);
      if (!control) continue;
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const values: Record<string, string> = {};
    for (const field of this.fields()) {
      const value = this.form.get(field.key)?.value;
      if (value === null || value === undefined || value === '') continue;
      values[field.key] = field.type === 'checkbox' ? String(!!value) : String(value);
    }

    this.submitted.emit(values);
  }

  #buildForm(fields: WebFormField[]): FormGroup {
    const group: Record<string, FormControl> = {};

    for (const field of fields) {
      const validators: ValidatorFn[] = [];
      if (field.required) validators.push(Validators.required);
      if (field.maxLength) validators.push(Validators.maxLength(field.maxLength));
      if (field.pattern) validators.push(patternValidator(field.pattern));

      group[field.key] = new FormControl(field.type === 'checkbox' ? false : '', validators);
    }

    return new FormGroup(group);
  }
}

/** A stray flag/anchor difference between the two engines is not worth failing a submission over — best-effort only. */
function patternValidator(pattern: string): ValidatorFn {
  let regex: RegExp | null = null;
  try {
    regex = new RegExp(pattern);
  } catch {
    return () => null;
  }

  return (control): ValidationErrors | null => {
    if (!control.value) return null;
    return regex!.test(String(control.value)) ? null : { pattern: true };
  };
}
