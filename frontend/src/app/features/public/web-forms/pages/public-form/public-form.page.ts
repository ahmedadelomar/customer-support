import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { ApiProblem } from '../../../../../core/models/api.models';
import { LanguageService } from '../../../../../core/services/language.service';
import { PublicWebFormsService } from '../../data-access/public-web-forms.service';
import type { PublicWebFormSchema, SubmitWebFormResult } from '../../data-access/interfaces/web-form.interface';
import { DynamicFormComponent } from '../../ui/dynamic-form/dynamic-form.component';

/**
 * The public page a form's key resolves to (Communication Channels / Web forms, CS-305) — the
 * plan's own "one-line embed" target, reachable directly or inside an iframe on any external site.
 */
@Component({
  selector: 'app-public-form',
  imports: [FormsModule, TranslatePipe, DynamicFormComponent],
  templateUrl: './public-form.page.html',
})
export class PublicFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #service = inject(PublicWebFormsService);
  readonly #language = inject(LanguageService);

  readonly dynamicForm = viewChild.required(DynamicFormComponent);

  readonly key = this.#route.snapshot.paramMap.get('key') ?? '';

  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly schema = signal<PublicWebFormSchema | null>(null);
  readonly submitting = signal(false);
  readonly result = signal<SubmitWebFormResult | null>(null);
  readonly captchaChecked = signal(false);

  readonly language = this.#language.current;

  constructor() {
    this.#service.getSchema(this.key).subscribe({
      next: (schema) => {
        this.schema.set(schema);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  pick(text: { en: string; ar: string }): string {
    return this.#language.pick(text);
  }

  toggleLanguage(): void {
    this.#language.toggle();
  }

  submit(values: Record<string, string>): void {
    const schema = this.schema();
    if (!schema) return;

    this.submitting.set(true);
    // No real captcha provider is wired up (see the backend's LoggingCaptchaVerifier) — this
    // checkbox is a placeholder for the real widget a provider integration would render here.
    const captchaToken = schema.requireCaptcha ? (this.captchaChecked() ? 'client-checkbox-ack' : null) : 'not-required';

    this.#service.submit(this.key, { values, captchaToken }).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.result.set(result);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.#applyServerErrors(error);
      },
    });
  }

  #applyServerErrors(error: unknown): void {
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
      return;
    }

    const problem = error.error as ApiProblem | undefined;
    if (problem?.errors) {
      this.dynamicForm().setServerErrors(problem.errors);
    }
  }
}
