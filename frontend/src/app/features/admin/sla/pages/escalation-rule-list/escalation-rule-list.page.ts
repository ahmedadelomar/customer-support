import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { EscalationRulesService } from '../../data-access/escalation-rules.service';
import type { EscalationRule } from '../../data-access/interfaces/escalation-rule.interface';
import { ESCALATION_ACTIONS, ESCALATION_TRIGGERS } from '../../data-access/interfaces/escalation-rule.interface';

/** Escalation rule administration (SLA and Automation / Escalation rules). */
@Component({
  selector: 'app-escalation-rule-list',
  imports: [DatePipe, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './escalation-rule-list.page.html',
})
export class EscalationRuleListPage {
  readonly #service = inject(EscalationRulesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly rules = signal<EscalationRule[]>([]);
  readonly loading = signal(true);
  readonly triggers = ESCALATION_TRIGGERS;
  readonly actions = ESCALATION_ACTIONS;

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

  name(rule: EscalationRule): string {
    return this.#language.pick({ en: rule.nameEn, ar: rule.nameAr });
  }

  triggerLabelKey(rule: EscalationRule): string {
    return this.triggers.find((t) => t.value === rule.trigger)?.labelKey ?? '';
  }

  actionLabelKey(rule: EscalationRule): string {
    return this.actions.find((a) => a.value === rule.action)?.labelKey ?? '';
  }

  toggleActive(rule: EscalationRule): void {
    this.#service
      .update(rule.id, {
        nameEn: rule.nameEn,
        nameAr: rule.nameAr,
        description: rule.description,
        evaluationOrder: rule.evaluationOrder,
        isActive: !rule.isActive,
        trigger: rule.trigger,
        thresholdPercent: rule.thresholdPercent,
        thresholdMinutes: rule.thresholdMinutes,
        thresholdCount: rule.thresholdCount,
        targetType: rule.targetType,
        conditions: rule.conditions,
        action: rule.action,
        actionTargetUserId: rule.actionTargetUserId,
        actionTargetTeamId: rule.actionTargetTeamId,
        actionTargetDepartmentId: rule.actionTargetDepartmentId,
        actionTargetPriorityId: rule.actionTargetPriorityId,
        actionNotifyRoleId: rule.actionNotifyRoleId,
        cooldownMinutes: rule.cooldownMinutes,
        maxFiresPerTicket: rule.maxFiresPerTicket,
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

  delete(rule: EscalationRule): void {
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
