import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { AutomationService } from '../../data-access/automation.service';
import type { AutomationRunLog } from '../../data-access/interfaces/automation-run-log.interface';

/**
 * The automation decision log (SLA and Automation / Automatic assignment and Escalation rules) —
 * "why did this go to Ahmed" and "why did this escalate" read the same log with a different filter.
 * Also reachable pre-filtered to one ticket from that ticket's history tab.
 */
@Component({
  selector: 'app-decision-log',
  imports: [DatePipe, FormsModule, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './decision-log.page.html',
})
export class DecisionLogPage {
  readonly #automation = inject(AutomationService);
  readonly #route = inject(ActivatedRoute);

  readonly logs = signal<AutomationRunLog[]>([]);
  readonly loading = signal(true);

  readonly ticketId = signal(this.#route.snapshot.queryParamMap.get('ticketId') ?? '');
  readonly ruleType = signal(this.#route.snapshot.queryParamMap.get('ruleType') ?? '');
  readonly outcome = signal('');

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#automation
      .log({
        ticketId: this.ticketId() || undefined,
        ruleType: this.ruleType() || undefined,
        outcome: this.outcome() || undefined,
      })
      .subscribe({
        next: (logs) => {
          this.logs.set(logs);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  clearFilters(): void {
    this.ticketId.set('');
    this.ruleType.set('');
    this.outcome.set('');
    this.load();
  }

  outcomeClasses(outcome: string): string {
    switch (outcome) {
      case 'Matched':
        return 'border-emerald-200 bg-emerald-50 text-emerald-700';
      case 'Failed':
        return 'border-rose-200 bg-rose-50 text-rose-700';
      default:
        return 'border-slate-200 bg-slate-100 text-slate-600';
    }
  }
}
