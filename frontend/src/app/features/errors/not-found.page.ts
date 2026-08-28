import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink, TranslatePipe, EmptyStateComponent],
  template: `
    <div class="flex min-h-screen items-center justify-center p-6">
      <div class="card w-full max-w-md">
        <app-empty-state titleKey="errors.notFoundTitle" descriptionKey="errors.notFound" icon="◌">
          <a class="btn-primary" routerLink="/agent/dashboard">{{ 'nav.dashboard' | translate }}</a>
        </app-empty-state>
      </div>
    </div>
  `,
})
export class NotFoundPage {}
