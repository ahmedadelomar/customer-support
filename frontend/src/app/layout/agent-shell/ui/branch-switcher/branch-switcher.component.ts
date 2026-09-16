import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/auth/auth.service';
import { LanguageService } from '../../../../core/services/language.service';
import { BranchesService, type Branch } from '../../../../features/admin/organization/data-access/branches.service';

/**
 * Active-branch picker in the top bar (Platform / Branch scoping and the branch switcher).
 *
 * Only rendered for users who can actually be in more than one branch: a single-branch agent would
 * see a dropdown with one immovable option, which is noise. Switching re-issues the token and
 * reloads the current route so every list refetches under the new scope — without the reload, lists
 * would keep showing the previous branch's rows until something else triggered a fetch.
 */
@Component({
  selector: 'app-branch-switcher',
  imports: [FormsModule],
  template: `
    @if (showSwitcher()) {
      <select
        class="form-control w-auto py-1.5 text-sm"
        [attr.aria-label]="'Branch'"
        [ngModel]="activeBranchId()"
        [disabled]="switching()"
        (ngModelChange)="switch($event)"
      >
        @for (branch of branches(); track branch.id) {
          <option [value]="branch.id">{{ name(branch) }}</option>
        }
      </select>
    }
  `,
})
export class BranchSwitcherComponent {
  readonly #branches = inject(BranchesService);
  readonly #auth = inject(AuthService);
  readonly #language = inject(LanguageService);
  readonly #router = inject(Router);

  readonly branches = signal<Branch[]>([]);
  readonly switching = signal(false);

  readonly activeBranchId = computed(() => this.#auth.user()?.branchId ?? '');

  /**
   * Unrestricted users (an empty accessible set — head office) may reach every branch; everyone else
   * only sees the switcher once they hold more than one.
   */
  readonly showSwitcher = computed(() => {
    const user = this.#auth.user();
    if (!user) return false;

    const unrestricted = (user.accessibleBranchIds?.length ?? 0) === 0;
    return this.branches().length > 1 && (unrestricted || user.accessibleBranchIds.length > 1);
  });

  constructor() {
    this.#branches.list().subscribe({
      next: (branches) => {
        const user = this.#auth.user();
        const accessible = user?.accessibleBranchIds ?? [];

        this.branches.set(
          accessible.length === 0 ? branches : branches.filter((b) => accessible.includes(b.id)),
        );
      },
      error: () => this.branches.set([]),
    });
  }

  name(branch: Branch): string {
    return this.#language.pick({ en: branch.nameEn, ar: branch.nameAr });
  }

  switch(branchId: string): void {
    if (!branchId || branchId === this.activeBranchId() || this.switching()) {
      return;
    }

    this.switching.set(true);

    this.#auth.switchBranch(branchId).subscribe({
      next: () => {
        this.switching.set(false);

        // Re-navigate to the current URL so resolvers and list loads run again under the new scope.
        const url = this.#router.url;
        void this.#router.navigateByUrl('/', { skipLocationChange: true })
          .then(() => this.#router.navigateByUrl(url));
      },
      error: () => this.switching.set(false),
    });
  }
}
