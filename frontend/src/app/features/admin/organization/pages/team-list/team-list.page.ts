import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { HasPermissionDirective } from '../../../../../core/directives/has-permission.directive';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { UsersService } from '../../../users/data-access/users.service';
import type { UserListItem } from '../../../users/data-access/interfaces/user.interface';
import {
  OrganizationService,
  type Department,
  type Team,
  type TeamMemberInput,
} from '../../data-access/organization.service';

/**
 * Teams admin with the membership editor (Platform / Departments, teams and queue scoping).
 *
 * The member list's ORDER is the round-robin rotation order — the server assigns `RotationOrder`
 * from the submitted sequence — so the up/down controls are functional, not cosmetic.
 */
@Component({
  selector: 'app-team-list',
  imports: [
    FormsModule,
    TranslatePipe,
    PageHeaderComponent,
    EmptyStateComponent,
    ModalComponent,
    HasPermissionDirective,
  ],
  templateUrl: './team-list.page.html',
})
export class TeamListPage {
  readonly #service = inject(OrganizationService);
  readonly #users = inject(UsersService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly permissions = PERMISSIONS;

  readonly teams = signal<Team[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly agents = signal<UserListItem[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly departmentFilter = signal('');
  readonly errorMessage = signal<string | null>(null);

  readonly formOpen = signal(false);
  readonly editing = signal<Team | null>(null);
  readonly departmentId = signal('');
  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly isActive = signal(true);

  readonly membersOpen = signal(false);
  readonly memberTeam = signal<Team | null>(null);
  readonly draftMembers = signal<TeamMemberInput[]>([]);
  readonly agentToAdd = signal('');

  readonly lang = computed(() => this.#language.current());

  /** Agents not already on the team being edited. */
  readonly addableAgents = computed(() => {
    const taken = new Set(this.draftMembers().map((m) => m.userId));
    return this.agents().filter((a) => !taken.has(a.id));
  });

  constructor() {
    this.#service.departments().subscribe({ next: (d) => this.departments.set(d) });
    this.#users.list({ pageSize: 200, isActive: true }).subscribe({
      next: (result) => this.agents.set(result.items),
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.teams(this.departmentFilter() || undefined, true).subscribe({
      next: (teams) => {
        this.teams.set(teams);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  teamName(team: Team): string {
    return this.#language.pick({ en: team.nameEn, ar: team.nameAr });
  }

  departmentName(department: Department): string {
    return this.#language.pick({ en: department.nameEn, ar: department.nameAr });
  }

  agentName(userId: string): string {
    const agent = this.agents().find((a) => a.id === userId);
    if (!agent) return userId;
    return this.#language.pick({ en: agent.displayNameEn, ar: agent.displayNameAr });
  }

  // --- Team form ------------------------------------------------------------------------------
  openCreate(): void {
    this.editing.set(null);
    this.departmentId.set(this.departments()[0]?.id ?? '');
    this.nameEn.set('');
    this.nameAr.set('');
    this.isActive.set(true);
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  openEdit(team: Team): void {
    this.editing.set(team);
    this.departmentId.set(team.departmentId);
    this.nameEn.set(team.nameEn);
    this.nameAr.set(team.nameAr);
    this.isActive.set(team.isActive);
    this.errorMessage.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    this.saving.set(true);
    this.errorMessage.set(null);

    const existing = this.editing();
    const request$: Observable<string | void> = existing
      ? this.#service.updateTeam(existing.id, {
          nameEn: this.nameEn(),
          nameAr: this.nameAr(),
          isActive: this.isActive(),
        })
      : this.#service.createTeam({
          departmentId: this.departmentId(),
          nameEn: this.nameEn(),
          nameAr: this.nameAr(),
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.#toast.success(existing ? 'admin.teams.updated' : 'admin.teams.created');
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  // --- Membership editor ----------------------------------------------------------------------
  openMembers(team: Team): void {
    this.memberTeam.set(team);
    this.draftMembers.set(
      team.members.map((m) => ({
        userId: m.userId,
        maxConcurrentTickets: m.maxConcurrentTickets,
        isLead: m.isLead,
      })),
    );
    this.agentToAdd.set('');
    this.errorMessage.set(null);
    this.membersOpen.set(true);
  }

  closeMembers(): void {
    this.membersOpen.set(false);
  }

  addMember(): void {
    const userId = this.agentToAdd();
    if (!userId) return;

    this.draftMembers.update((current) => [
      ...current,
      { userId, maxConcurrentTickets: 25, isLead: current.length === 0 },
    ]);
    this.agentToAdd.set('');
  }

  removeMember(userId: string): void {
    this.draftMembers.update((current) => current.filter((m) => m.userId !== userId));
  }

  setCapacity(userId: string, value: string): void {
    const capacity = Number(value);
    this.draftMembers.update((current) =>
      current.map((m) => (m.userId === userId ? { ...m, maxConcurrentTickets: capacity } : m)),
    );
  }

  setLead(userId: string): void {
    this.draftMembers.update((current) =>
      current.map((m) => ({ ...m, isLead: m.userId === userId })),
    );
  }

  /** Moves a member up or down; the resulting order becomes the rotation order on save. */
  move(index: number, delta: number): void {
    this.draftMembers.update((current) => {
      const target = index + delta;
      if (target < 0 || target >= current.length) return current;

      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  saveMembers(): void {
    const team = this.memberTeam();
    if (!team) return;

    this.saving.set(true);
    this.errorMessage.set(null);

    this.#service.updateMembers(team.id, this.draftMembers()).subscribe({
      next: () => {
        this.saving.set(false);
        this.membersOpen.set(false);
        this.#toast.success('admin.teams.membersSaved');
        this.load();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(this.#extractMessage(error));
      },
    });
  }

  #extractMessage(error: unknown): string | null {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { detail?: string; errors?: Record<string, string[]> } }).error;
      if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
      if (problem?.detail) return problem.detail;
    }
    return null;
  }
}
