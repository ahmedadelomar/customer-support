import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import type { Department, Team } from '../../../organization/data-access/organization.service';
import { OrganizationService } from '../../../organization/data-access/organization.service';
import type { Role } from '../../../roles/data-access/roles.service';
import { RolesService } from '../../../roles/data-access/roles.service';
import type { UserListItem } from '../../../users/data-access/interfaces/user.interface';
import { UsersService } from '../../../users/data-access/users.service';
import { AutomationService } from '../../data-access/automation.service';
import { EscalationRulesService } from '../../data-access/escalation-rules.service';
import type {
  EscalationActionType,
  EscalationTestMatch,
  EscalationTrigger,
  SlaTargetTypeFilter,
} from '../../data-access/interfaces/escalation-rule.interface';
import { ESCALATION_ACTIONS, ESCALATION_TRIGGERS } from '../../data-access/interfaces/escalation-rule.interface';
import type { ConditionRow } from '../../data-access/interfaces/sla-policy.interface';
import { ConditionBuilderComponent } from '../../ui/condition-builder/condition-builder.component';

/**
 * Create/edit an escalation rule. The trigger switches which threshold input is shown (percent,
 * minutes or count) and the action switches its target field — an administrator can never set a
 * percentage on a count-based trigger, because that input simply is not there.
 */
@Component({
  selector: 'app-escalation-rule-form',
  imports: [FormsModule, RouterLink, TranslatePipe, PageHeaderComponent, ConditionBuilderComponent],
  templateUrl: './escalation-rule-form.page.html',
})
export class EscalationRuleFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #service = inject(EscalationRulesService);
  readonly #automation = inject(AutomationService);
  readonly #organization = inject(OrganizationService);
  readonly #users = inject(UsersService);
  readonly #roles = inject(RolesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly ruleId = this.#route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.ruleId;

  readonly loading = signal(this.isEdit);
  readonly saving = signal(false);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly description = signal('');
  readonly evaluationOrder = signal(100);
  readonly isActive = signal(true);
  readonly trigger = signal<EscalationTrigger>(0);
  readonly thresholdPercent = signal<number | null>(80);
  readonly thresholdMinutes = signal<number | null>(120);
  readonly thresholdCount = signal<number | null>(3);
  readonly targetType = signal<SlaTargetTypeFilter>(null);
  readonly conditions = signal<ConditionRow[]>([]);
  readonly action = signal<EscalationActionType>(0);
  readonly actionTargetUserId = signal('');
  readonly actionTargetTeamId = signal('');
  readonly actionTargetDepartmentId = signal('');
  readonly actionNotifyRoleId = signal('');
  readonly cooldownMinutes = signal(60);
  readonly maxFiresPerTicket = signal(3);

  readonly conditionFields = signal<string[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly users = signal<UserListItem[]>([]);
  readonly roles = signal<Role[]>([]);

  readonly triggers = ESCALATION_TRIGGERS;
  readonly actions = ESCALATION_ACTIONS;

  readonly currentTrigger = computed(() => this.triggers.find((t) => t.value === this.trigger()));
  readonly currentAction = computed(() => this.actions.find((a) => a.value === this.action()));

  // --- Tester (dry run — lists tickets that would fire, nothing is written) -------------------
  readonly testerResults = signal<EscalationTestMatch[] | null>(null);
  readonly testerRunning = signal(false);

  constructor() {
    this.#automation.conditionFields().subscribe((fields) => this.conditionFields.set(fields));
    this.#organization.departments().subscribe((departments) => this.departments.set(departments));
    this.#organization.teams().subscribe((teams) => this.teams.set(teams));
    this.#users.list({ pageSize: 200 }).subscribe((result) => this.users.set(result.items));
    this.#roles.list().subscribe((roles) => this.roles.set(roles));

    if (this.ruleId) {
      this.#service.list().subscribe({
        next: (rules) => {
          const rule = rules.find((r) => r.id === this.ruleId);
          if (!rule) {
            this.#toast.error('common.notFound');
            void this.#router.navigate(['/admin/sla/escalation-rules']);
            return;
          }

          this.nameEn.set(rule.nameEn);
          this.nameAr.set(rule.nameAr);
          this.description.set(rule.description ?? '');
          this.evaluationOrder.set(rule.evaluationOrder);
          this.isActive.set(rule.isActive);
          this.trigger.set(rule.trigger);
          this.thresholdPercent.set(rule.thresholdPercent);
          this.thresholdMinutes.set(rule.thresholdMinutes);
          this.thresholdCount.set(rule.thresholdCount);
          this.targetType.set(rule.targetType);
          this.conditions.set(rule.conditions);
          this.action.set(rule.action);
          this.actionTargetUserId.set(rule.actionTargetUserId ?? '');
          this.actionTargetTeamId.set(rule.actionTargetTeamId ?? '');
          this.actionTargetDepartmentId.set(rule.actionTargetDepartmentId ?? '');
          this.actionNotifyRoleId.set(rule.actionNotifyRoleId ?? '');
          this.cooldownMinutes.set(rule.cooldownMinutes);
          this.maxFiresPerTicket.set(rule.maxFiresPerTicket);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  userName(user: UserListItem): string {
    return this.#language.pick({ en: user.displayNameEn, ar: user.displayNameAr });
  }

  departmentName(department: Department): string {
    return this.#language.pick({ en: department.nameEn, ar: department.nameAr });
  }

  teamName(team: Team): string {
    return this.#language.pick({ en: team.nameEn, ar: team.nameAr });
  }

  roleName(role: Role): string {
    return this.#language.pick({ en: role.displayNameEn, ar: role.displayNameAr });
  }

  save(): void {
    if (!this.nameEn().trim() || !this.nameAr().trim()) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const request = {
      nameEn: this.nameEn(),
      nameAr: this.nameAr(),
      description: this.description() || null,
      evaluationOrder: this.evaluationOrder(),
      isActive: this.isActive(),
      trigger: this.trigger(),
      thresholdPercent: this.currentTrigger()?.thresholdKind === 'percent' ? this.thresholdPercent() : null,
      thresholdMinutes: this.currentTrigger()?.thresholdKind === 'minutes' ? this.thresholdMinutes() : null,
      thresholdCount: this.currentTrigger()?.thresholdKind === 'count' ? this.thresholdCount() : null,
      targetType: this.targetType(),
      conditions: this.conditions(),
      action: this.action(),
      actionTargetUserId: this.actionTargetUserId() || null,
      actionTargetTeamId: this.actionTargetTeamId() || null,
      actionTargetDepartmentId: this.actionTargetDepartmentId() || null,
      actionTargetPriorityId: null,
      actionNotifyRoleId: this.actionNotifyRoleId() || null,
      cooldownMinutes: this.cooldownMinutes(),
      maxFiresPerTicket: this.maxFiresPerTicket(),
    };

    const request$: Observable<string | void> = this.ruleId
      ? this.#service.update(this.ruleId, request)
      : this.#service.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.ruleId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/sla/escalation-rules']);
      },
      error: () => this.saving.set(false),
    });
  }

  runTest(): void {
    if (!this.ruleId) return;

    this.testerRunning.set(true);
    this.#service.test(this.ruleId).subscribe({
      next: (matches) => {
        this.testerResults.set(matches);
        this.testerRunning.set(false);
      },
      error: () => this.testerRunning.set(false),
    });
  }
}
