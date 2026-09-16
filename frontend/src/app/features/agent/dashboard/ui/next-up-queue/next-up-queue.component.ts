import { Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { SlaBadgeComponent } from '../../../../../shared/ui/sla-badge/sla-badge.component';
import type { AgentQueueItem } from '../../data-access/interfaces/agent-dashboard.interface';

/**
 * The next-up queue: compact rows ordered by urgency (breached, then remaining time, then
 * priority, then age — computed server-side, this component only renders the given order).
 * A presentational child so the page can update `items` in place on every refresh poll without
 * this component itself needing any refresh-specific logic — `@for`'s `track` keeps row identity
 * (and therefore scroll position, any open native tooltip, etc.) stable across updates.
 */
@Component({
  selector: 'app-next-up-queue',
  imports: [RouterLink, TranslatePipe, EmptyStateComponent, SlaBadgeComponent],
  templateUrl: './next-up-queue.component.html',
})
export class NextUpQueueComponent {
  readonly #language = inject(LanguageService);

  readonly items = input.required<AgentQueueItem[]>();

  customerName(item: AgentQueueItem): string {
    return this.#language.pick({ en: item.customerDisplayNameEn, ar: item.customerDisplayNameAr });
  }

  priorityName(item: AgentQueueItem): string {
    return this.#language.pick({ en: item.priorityNameEn, ar: item.priorityNameAr });
  }
}
