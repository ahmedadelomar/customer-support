import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Forced (or voluntary) password change. An administrator-created account lands here on its first
 * sign-in: until the password is replaced, the API refuses every other call with
 * `code: must_change_password`, and `errorInterceptor` routes here rather than showing a toast the
 * user cannot act on.
 */
@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule, TranslatePipe],
  template: `
    <div class="flex min-h-screen items-center justify-center bg-slate-100 p-4">
      <div class="card w-full max-w-sm p-6">
        <div class="mb-6 text-center">
          <span
            class="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-brand-700 text-lg font-bold text-white"
          >
            CS
          </span>
          <h1 class="text-lg font-semibold text-slate-900">{{ 'auth.changePassword.title' | translate }}</h1>
          <p class="mt-1 text-sm text-slate-500">{{ 'auth.changePassword.subtitle' | translate }}</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="mb-4">
            <label class="form-label" for="currentPassword">{{ 'auth.changePassword.current' | translate }}</label>
            <input
              id="currentPassword"
              type="password"
              class="form-control"
              formControlName="currentPassword"
              autocomplete="current-password"
            />
            @for (message of fieldErrors('currentPassword'); track message) {
              <p class="form-error mt-1">{{ message }}</p>
            }
          </div>

          <div class="mb-4">
            <label class="form-label" for="newPassword">{{ 'auth.changePassword.new' | translate }}</label>
            <input
              id="newPassword"
              type="password"
              class="form-control"
              formControlName="newPassword"
              autocomplete="new-password"
            />
            @for (message of fieldErrors('newPassword'); track message) {
              <p class="form-error mt-1">{{ message }}</p>
            }
          </div>

          <div class="mb-4">
            <label class="form-label" for="confirmPassword">{{ 'auth.changePassword.confirm' | translate }}</label>
            <input
              id="confirmPassword"
              type="password"
              class="form-control"
              formControlName="confirmPassword"
              autocomplete="new-password"
            />
            @if (mismatch()) {
              <p class="form-error mt-1">{{ 'auth.changePassword.mismatch' | translate }}</p>
            }
          </div>

          <button type="submit" class="btn-primary w-full" [disabled]="saving() || form.invalid">
            {{ (saving() ? 'common.saving' : 'auth.changePassword.submit') | translate }}
          </button>
        </form>

        <button type="button" class="mt-3 w-full text-sm text-slate-500 hover:underline" (click)="signOut()">
          {{ 'auth.logout' | translate }}
        </button>
      </div>
    </div>
  `,
})
export class ChangePasswordPage {
  readonly #auth = inject(AuthService);
  readonly #router = inject(Router);
  readonly #fb = inject(FormBuilder);

  readonly saving = signal(false);
  readonly mismatch = signal(false);
  readonly errors = signal<Record<string, string[]>>({});

  readonly form = this.#fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  fieldErrors(field: string): string[] {
    return this.errors()[field]?.filter((message) => message.length > 0) ?? [];
  }

  submit(): void {
    const { currentPassword, newPassword, confirmPassword } = this.form.getRawValue();

    this.mismatch.set(newPassword !== confirmPassword);
    if (this.form.invalid || this.mismatch()) {
      return;
    }

    this.saving.set(true);
    this.errors.set({});

    this.#auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.saving.set(false);
        void this.#router.navigate(['/agent/dashboard']);
      },
      error: (error: unknown) => {
        this.saving.set(false);

        if (error && typeof error === 'object' && 'error' in error) {
          const problem = (error as { error?: { errors?: Record<string, string[]> } }).error;
          if (problem?.errors) this.errors.set(problem.errors);
        }
      },
    });
  }

  signOut(): void {
    this.#auth.logout();
  }
}
