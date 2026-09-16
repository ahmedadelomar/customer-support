import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { AssignmentRulesService } from '../../data-access/assignment-rules.service';
import type { AssignmentRule } from '../../data-access/interfaces/assignment-rule.interface';
import { ASSIGNMENT_STRATEGIES } from '../../data-access/interfaces/assignment-rule.interface';

/**
 * Assignment rule administration (SLA and Automation / Automatic assignment). Ordered top-to-bottom
 * by `evaluationOrder` — the order rules are actually tried, first match wins — with up/down moves
 * that rewrite it, the same functional-not-decorative pattern as the team member editor.
 */
@Component({
  selector: 'app-assignment-rule-list',
  imports: [DatePipe, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './assignment-rule-list.page.html',
})
export class AssignmentRuleListPage {
  readonly #service = inject(AssignmentRulesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly rules = signal<AssignmentRule[]>([]);
  readonly loading = signal(true);
  readonly strategies = ASSIGNMENT_STRATEGIES;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (rules) => {
        this.rules.set(rules);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(rule: AssignmentRule): string {
    return this.#language.pick({ en: rule.nameEn, ar: rule.nameAr });
  }

  strategyLabelKey(rule: AssignmentRule): string {
    return this.strategies.find((s) => s.value === rule.strategy)?.labelKey ?? '';
  }

  toggleActive(rule: AssignmentRule): void {
    this.#service
      .update(rule.id, {
        nameEn: rule.nameEn,
        nameAr: rule.nameAr,
        description: rule.description,
        evaluationOrder: rule.evaluationOrder,
        isActive: !rule.isActive,
        conditions: rule.conditions,
        strategy: rule.strategy,
        targetDepartmentId: rule.targetDepartmentId,
        targetTeamId: rule.targetTeamId,
        targetUserId: rule.targetUserId,
        respectAgentAvailability: rule.respectAgentAvailability,
        stopProcessing: rule.stopProcessing,
      })
      .subscribe({ next: () => this.load() });
  }

  moveUp(index: number): void {
    if (index === 0) return;
    this.#swapAndReorder(index, index - 1);
  }

  moveDown(index: number): void {
    if (index === this.rules().length - 1) return;
    this.#swapAndReorder(index, index + 1);
  }

  delete(rule: AssignmentRule): void {
    if (!confirm(`Delete "${this.name(rule)}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(rule.id).subscribe({
      next: () => {
        this.#toast.success('common.deleted');
        this.load();
      },
    });
  }

  #swapAndReorder(a: number, b: number): void {
    const items = [...this.rules()];
    [items[a], items[b]] = [items[b], items[a]];
    this.rules.set(items);

    this.#service.reorder(items.map((r) => r.id)).subscribe({ error: () => this.load() });
  }
}
