import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { SlaPoliciesService } from '../../data-access/sla-policies.service';
import type { SlaPolicy } from '../../data-access/interfaces/sla-policy.interface';

/**
 * SLA policy administration (SLA and Automation / Response and resolution targets). Ordered by
 * `evaluationOrder`; the default policy is pinned last as the documented fallback rather than
 * reorderable into the match sequence.
 */
@Component({
  selector: 'app-sla-policy-list',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './policy-list.page.html',
})
export class PolicyListPage {
  readonly #service = inject(SlaPoliciesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  readonly policies = signal<SlaPolicy[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (policies) => {
        this.policies.set(policies);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(policy: SlaPolicy): string {
    return this.#language.pick({ en: policy.nameEn, ar: policy.nameAr });
  }

  calendarName(policy: SlaPolicy): string {
    return this.#language.pick({ en: policy.calendarNameEn, ar: policy.calendarNameAr });
  }

  edit(policy: SlaPolicy): void {
    void this.#router.navigate(['/admin/sla/policies', policy.id]);
  }

  delete(policy: SlaPolicy): void {
    if (!confirm(`Delete "${this.name(policy)}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(policy.id).subscribe({
      next: () => {
        this.#toast.success('common.deleted');
        this.load();
      },
    });
  }
}
