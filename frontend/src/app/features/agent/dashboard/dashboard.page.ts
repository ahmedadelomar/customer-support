import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../core/auth/auth.service';
import { LanguageService } from '../../../core/services/language.service';
import { PageHeaderComponent } from '../../../shared/ui/page-header/page-header.component';
import { StateCardComponent } from '../../../shared/ui/state-card/state-card.component';

/**
 * Agent home. Tiles and queues are wired to real endpoints by the Agent Dashboard stories;
 * the shell exists now so the shipped app has a working landing page.
 */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent, StateCardComponent],
  template: `
    <app-page-header titleKey="dashboard.title" [subtitleKey]="null" />

    <p class="mb-5 text-sm text-slate-600">
      {{ 'dashboard.welcome' | translate: { name: displayName() } }}
    </p>

    <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <app-state-card labelKey="dashboard.tiles.assigned" icon="✉" hintKey="common.comingSoon" />
      <app-state-card
        labelKey="dashboard.tiles.dueToday"
        icon="◷"
        hintKey="common.comingSoon"
        badgeClasses="bg-amber-50 text-amber-700"
      />
      <app-state-card
        labelKey="dashboard.tiles.breaching"
        icon="⚠"
        hintKey="common.comingSoon"
        badgeClasses="bg-rose-50 text-rose-700"
      />
      <app-state-card
        labelKey="dashboard.tiles.resolvedThisWeek"
        icon="✓"
        hintKey="common.comingSoon"
        badgeClasses="bg-emerald-50 text-emerald-700"
      />
    </div>

    <div class="mt-6 flex flex-wrap gap-2">
      <a class="btn-primary" routerLink="/agent/customers">{{ 'nav.customers' | translate }}</a>
      <a class="btn-secondary" routerLink="/agent/tickets">{{ 'nav.tickets' | translate }}</a>
    </div>
  `,
})
export class DashboardPage {
  readonly #auth = inject(AuthService);
  readonly #language = inject(LanguageService);

  displayName(): string {
    const user = this.#auth.user();
    return user ? this.#language.pick({ en: user.displayNameEn, ar: user.displayNameAr }) : '';
  }
}
