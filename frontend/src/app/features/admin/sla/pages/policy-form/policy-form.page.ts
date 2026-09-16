import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { TicketPrioritiesService } from '../../../priorities/data-access/ticket-priorities.service';
import type { TicketPriorityAdmin } from '../../../priorities/data-access/interfaces/ticket-priority.interface';
import { AutomationService } from '../../data-access/automation.service';
import { BusinessCalendarsService } from '../../data-access/business-calendars.service';
import { SlaPoliciesService } from '../../data-access/sla-policies.service';
import type { BusinessCalendar } from '../../data-access/interfaces/business-calendar.interface';
import type { ConditionRow, SlaPolicy, SlaPolicyPreviewResult, SlaTarget } from '../../data-access/interfaces/sla-policy.interface';
import { ConditionBuilderComponent } from '../../ui/condition-builder/condition-builder.component';

interface TargetRow {
  priorityId: string;
  priorityName: string;
  firstResponseMinutes: number;
  resolutionMinutes: number;
}

/**
 * Create/edit an SLA policy: name, calendar, evaluation order, conditions, per-priority targets,
 * and a preview tool so a manager can sanity-check due dates before a real ticket depends on them.
 */
@Component({
  selector: 'app-sla-policy-form',
  imports: [DatePipe, FormsModule, RouterLink, TranslatePipe, PageHeaderComponent, ConditionBuilderComponent],
  templateUrl: './policy-form.page.html',
})
export class PolicyFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #policies = inject(SlaPoliciesService);
  readonly #calendars = inject(BusinessCalendarsService);
  readonly #priorities = inject(TicketPrioritiesService);
  readonly #automation = inject(AutomationService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly policyId = this.#route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.policyId;

  readonly loading = signal(true);
  readonly saving = signal(false);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly description = signal('');
  readonly businessCalendarId = signal('');
  readonly evaluationOrder = signal(100);
  readonly isDefault = signal(false);
  readonly isActive = signal(true);
  readonly warningThresholdPercent = signal(80);
  readonly pauseOnPendingCustomer = signal(true);
  readonly conditions = signal<ConditionRow[]>([]);
  readonly targets = signal<TargetRow[]>([]);

  readonly calendars = signal<BusinessCalendar[]>([]);
  readonly conditionFields = signal<string[]>([]);

  readonly previewPriorityId = signal('');
  readonly previewArrival = signal(new Date().toISOString().slice(0, 16));
  readonly previewResult = signal<SlaPolicyPreviewResult | null>(null);
  readonly previewing = signal(false);

  readonly calendarOptions = computed(() =>
    this.calendars().map((c) => ({ id: c.id, name: this.#language.pick({ en: c.nameEn, ar: c.nameAr }) })),
  );

  constructor() {
    this.#loadReferenceData();
  }

  #loadReferenceData(): void {
    this.#priorities.list().subscribe((priorities) => {
      const active = priorities.filter((p) => p.isActive);
      if (this.policyId) {
        this.#loadPolicy(active);
      } else {
        this.targets.set(active.map((p) => this.#emptyRow(p)));
        this.loading.set(false);
      }
    });

    this.#calendars.list().subscribe((calendars) => {
      this.calendars.set(calendars);
      if (!this.businessCalendarId()) {
        const preferred = calendars.find((c) => c.isDefault) ?? calendars[0];
        if (preferred) this.businessCalendarId.set(preferred.id);
      }
    });

    this.#automation.conditionFields().subscribe((fields) => this.conditionFields.set(fields));
  }

  #emptyRow(priority: TicketPriorityAdmin): TargetRow {
    return {
      priorityId: priority.id,
      priorityName: this.#language.pick({ en: priority.nameEn, ar: priority.nameAr }),
      firstResponseMinutes: 0,
      resolutionMinutes: 0,
    };
  }

  #loadPolicy(activePriorities: TicketPriorityAdmin[]): void {
    this.#policies.list().subscribe({
      next: (policies) => {
        const policy = policies.find((p) => p.id === this.policyId);
        if (!policy) {
          this.#toast.error('common.notFound');
          void this.#router.navigate(['/admin/sla/policies']);
          return;
        }

        this.#applyPolicy(policy, activePriorities);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  #applyPolicy(policy: SlaPolicy, activePriorities: TicketPriorityAdmin[]): void {
    this.nameEn.set(policy.nameEn);
    this.nameAr.set(policy.nameAr);
    this.description.set(policy.description ?? '');
    this.businessCalendarId.set(policy.businessCalendarId);
    this.evaluationOrder.set(policy.evaluationOrder);
    this.isDefault.set(policy.isDefault);
    this.isActive.set(policy.isActive);
    this.warningThresholdPercent.set(policy.warningThresholdPercent);
    this.pauseOnPendingCustomer.set(policy.pauseOnPendingCustomer);
    this.conditions.set(policy.conditions);

    const existing = new Map(policy.targets.map((t) => [t.priorityId, t]));
    this.targets.set(
      activePriorities.map((p) => {
        const target: SlaTarget | undefined = existing.get(p.id);
        return {
          priorityId: p.id,
          priorityName: this.#language.pick({ en: p.nameEn, ar: p.nameAr }),
          firstResponseMinutes: target?.firstResponseMinutes ?? 0,
          resolutionMinutes: target?.resolutionMinutes ?? 0,
        };
      }),
    );
  }

  updateTarget(index: number, field: 'firstResponseMinutes' | 'resolutionMinutes', value: number): void {
    this.targets.update((rows) => rows.map((r, i) => (i === index ? { ...r, [field]: value } : r)));
  }

  humanDuration(minutes: number): string {
    if (!minutes) return '';
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours === 0) return `${mins}m`;
    if (mins === 0) return `${hours}h`;
    return `${hours}h ${mins}m`;
  }

  save(): void {
    if (!this.nameEn().trim() || !this.nameAr().trim() || !this.businessCalendarId()) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const request = {
      nameEn: this.nameEn(),
      nameAr: this.nameAr(),
      description: this.description() || null,
      businessCalendarId: this.businessCalendarId(),
      evaluationOrder: this.evaluationOrder(),
      isDefault: this.isDefault(),
      isActive: this.isActive(),
      warningThresholdPercent: this.warningThresholdPercent(),
      pauseOnPendingCustomer: this.pauseOnPendingCustomer(),
      conditions: this.conditions(),
      targets: this.targets()
        .filter((t) => t.firstResponseMinutes > 0 || t.resolutionMinutes > 0)
        .map((t) => ({ priorityId: t.priorityId, firstResponseMinutes: t.firstResponseMinutes, resolutionMinutes: t.resolutionMinutes })),
    };

    const request$: Observable<string | void> = this.policyId
      ? this.#policies.update(this.policyId, request)
      : this.#policies.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.policyId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/sla/policies']);
      },
      error: () => this.saving.set(false),
    });
  }

  runPreview(): void {
    if (!this.policyId || !this.previewPriorityId()) {
      return;
    }

    this.previewing.set(true);
    const arrival = new Date(this.previewArrival()).toISOString();

    this.#policies.preview(this.policyId, this.previewPriorityId(), arrival).subscribe({
      next: (result) => {
        this.previewResult.set(result);
        this.previewing.set(false);
      },
      error: () => this.previewing.set(false),
    });
  }
}
