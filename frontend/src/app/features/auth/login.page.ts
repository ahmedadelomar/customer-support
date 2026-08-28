import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/services/language.service';

/** Sign-in for agents and portal users. Returns the user to the page they were blocked from. */
@Component({
  selector: 'app-login',
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
          <h1 class="text-lg font-semibold text-slate-900">{{ 'app.name' | translate }}</h1>
          <p class="mt-1 text-sm text-slate-500">{{ 'auth.signInSubtitle' | translate }}</p>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="mb-4">
            <label class="form-label" for="userName">{{ 'auth.userName' | translate }}</label>
            <input id="userName" type="text" class="form-control" formControlName="userName" autocomplete="username" />
          </div>

          <div class="mb-4">
            <label class="form-label" for="password">{{ 'auth.password' | translate }}</label>
            <input
              id="password"
              type="password"
              class="form-control"
              formControlName="password"
              autocomplete="current-password"
            />
          </div>

          @if (errorKey()) {
            <p class="mb-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {{ errorKey()! | translate }}
            </p>
          }

          <button type="submit" class="btn-primary w-full" [disabled]="submitting() || form.invalid">
            {{ (submitting() ? 'auth.signingIn' : 'auth.signIn') | translate }}
          </button>
        </form>

        <button type="button" class="mt-4 w-full text-sm text-slate-500 hover:text-slate-800" (click)="toggleLanguage()">
          {{ 'common.switchLanguage' | translate }}
        </button>
      </div>
    </div>
  `,
})
export class LoginPage {
  readonly #fb = inject(FormBuilder);
  readonly #auth = inject(AuthService);
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #language = inject(LanguageService);

  readonly submitting = signal(false);
  readonly errorKey = signal<string | null>(null);

  readonly form = this.#fb.nonNullable.group({
    userName: ['', Validators.required],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.submitting.set(true);
    this.errorKey.set(null);

    this.#auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        const returnUrl = this.#route.snapshot.queryParamMap.get('returnUrl') ?? '/agent/dashboard';
        void this.#router.navigateByUrl(returnUrl);
      },
      error: () => {
        this.submitting.set(false);
        // Deliberately generic: a specific message would let an attacker enumerate user names.
        this.errorKey.set('auth.invalidCredentials');
      },
    });
  }

  toggleLanguage(): void {
    this.#language.toggle();
  }
}
