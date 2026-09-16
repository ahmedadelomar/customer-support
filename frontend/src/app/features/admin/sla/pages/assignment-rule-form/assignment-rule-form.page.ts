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
import type { UserListItem } from '../../../users/data-access/interfaces/user.interface';
import { UsersService } from '../../../users/data-access/users.service';
import { TicketsService } from '../../../../agent/tickets/data-access/tickets.service';
import type { TicketListItem } from '../../../../agent/tickets/data-access/interfaces/ticket.interface';
import { AssignmentRulesService } from '../../data-access/assignment-rules.service';
import { AutomationService } from '../../data-access/automation.service';
import type { AssignmentPreview, AssignmentStrategy } from '../../data-access/interfaces/assignment-rule.interface';
import { ASSIGNMENT_STRATEGIES } from '../../data-access/interfaces/assignment-rule.interface';
import type { ConditionRow } from '../../data-access/interfaces/sla-policy.interface';
import { ConditionBuilderComponent } from '../../ui/condition-builder/condition-builder.component';

/**
 * Create/edit an assignment rule: conditions, strategy (whose target field switches per strategy),
 * availability/stop-processing toggles, and an embedded read-only tester against a real ticket.
 */
@Component({
  selector: 'app-assignment-rule-form',
  imports: [FormsModule, RouterLink, TranslatePipe, PageHeaderComponent, ConditionBuilderComponent],
  templateUrl: './assignment-rule-form.page.html',
})
export class AssignmentRuleFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #service = inject(AssignmentRulesService);
  readonly #automation = inject(AutomationService);
  readonly #organization = inject(OrganizationService);
  readonly #users = inject(UsersService);
  readonly #tickets = inject(TicketsService);
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
  readonly conditions = signal<ConditionRow[]>([]);
  readonly strategy = signal<AssignmentStrategy>(0);
  readonly targetDepartmentId = signal('');
  readonly targetTeamId = signal('');
  readonly targetUserId = signal('');
  readonly respectAgentAvailability = signal(true);
  readonly stopProcessing = signal(true);

  readonly conditionFields = signal<string[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly teams = signal<Team[]>([]);
  readonly users = signal<UserListItem[]>([]);

  readonly strategies = ASSIGNMENT_STRATEGIES;
  readonly currentStrategyExplainKey = computed(
    () => this.strategies.find((s) => s.value === this.strategy())?.explainKey ?? '',
  );
  readonly needsTeamOrDepartment = computed(() => [0, 1, 2].includes(this.strategy()));
  readonly needsUser = computed(() => this.strategy() === 3);
  readonly filteredTeams = computed(() =>
    this.targetDepartmentId() ? this.teams().filter((t) => t.departmentId === this.targetDepartmentId()) : this.teams(),
  );

  // --- Tester ---------------------------------------------------------------------------------
  readonly testerSearch = signal('');
  readonly testerResults = signal<TicketListItem[]>([]);
  readonly testerSelectedTicket = signal<TicketListItem | null>(null);
  readonly testerResult = signal<AssignmentPreview | null>(null);
  readonly testerRunning = signal(false);

  constructor() {
    this.#automation.conditionFields().subscribe((fields) => this.conditionFields.set(fields));
    this.#organization.departments().subscribe((departments) => this.departments.set(departments));
    this.#organization.teams().subscribe((teams) => this.teams.set(teams));
    this.#users.list({ pageSize: 200 }).subscribe((result) => this.users.set(result.items));

    if (this.ruleId) {
      this.#service.list().subscribe({
        next: (rules) => {
          const rule = rules.find((r) => r.id === this.ruleId);
          if (!rule) {
            this.#toast.error('common.notFound');
            void this.#router.navigate(['/admin/sla/assignment-rules']);
            return;
          }

          this.nameEn.set(rule.nameEn);
          this.nameAr.set(rule.nameAr);
          this.description.set(rule.description ?? '');
          this.evaluationOrder.set(rule.evaluationOrder);
          this.isActive.set(rule.isActive);
          this.conditions.set(rule.conditions);
          this.strategy.set(rule.strategy);
          this.targetDepartmentId.set(rule.targetDepartmentId ?? '');
          this.targetTeamId.set(rule.targetTeamId ?? '');
          this.targetUserId.set(rule.targetUserId ?? '');
          this.respectAgentAvailability.set(rule.respectAgentAvailability);
          this.stopProcessing.set(rule.stopProcessing);
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
      conditions: this.conditions(),
      strategy: this.strategy(),
      targetDepartmentId: this.targetDepartmentId() || null,
      targetTeamId: this.targetTeamId() || null,
      targetUserId: this.targetUserId() || null,
      respectAgentAvailability: this.respectAgentAvailability(),
      stopProcessing: this.stopProcessing(),
    };

    const request$: Observable<string | void> = this.ruleId
      ? this.#service.update(this.ruleId, request)
      : this.#service.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.ruleId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/sla/assignment-rules']);
      },
      error: () => this.saving.set(false),
    });
  }

  searchTickets(): void {
    if (!this.testerSearch().trim()) return;

    this.#tickets.list({ search: this.testerSearch(), assignment: 'all', page: 1, pageSize: 5 }).subscribe((result) => {
      this.testerResults.set(result.items);
    });
  }

  selectTicket(ticket: TicketListItem): void {
    this.testerSelectedTicket.set(ticket);
    this.testerResults.set([]);
    this.testerResult.set(null);
  }

  runTest(): void {
    const ticket = this.testerSelectedTicket();
    if (!this.ruleId || !ticket) return;

    this.testerRunning.set(true);
    this.#service.test(this.ruleId, ticket.id).subscribe({
      next: (result) => {
        this.testerResult.set(result);
        this.testerRunning.set(false);
      },
      error: () => this.testerRunning.set(false),
    });
  }
}
