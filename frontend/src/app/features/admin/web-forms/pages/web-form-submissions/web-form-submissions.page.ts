import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { WebFormsService } from '../../data-access/web-forms.service';
import type { WebFormSubmission } from '../../data-access/interfaces/web-form-admin.interface';

/** Submissions for one web form, including failures and their retry action (Communication Channels / Web forms, CS-305). */
@Component({
  selector: 'app-web-form-submissions',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent, EmptyStateComponent],
  templateUrl: './web-form-submissions.page.html',
})
export class WebFormSubmissionsPage {
  readonly #route = inject(ActivatedRoute);
  readonly #service = inject(WebFormsService);
  readonly #toast = inject(ToastService);

  readonly formId = this.#route.snapshot.paramMap.get('id')!;

  readonly submissions = signal<WebFormSubmission[]>([]);
  readonly loading = signal(true);
  readonly retryingId = signal<string | null>(null);
  readonly expandedId = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.submissions(this.formId).subscribe({
      next: (submissions) => {
        this.submissions.set(submissions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleExpanded(submission: WebFormSubmission): void {
    this.expandedId.set(this.expandedId() === submission.id ? null : submission.id);
  }

  formattedPayload(submission: WebFormSubmission): string {
    try {
      return JSON.stringify(JSON.parse(submission.payloadJson), null, 2);
    } catch {
      return submission.payloadJson;
    }
  }

  retry(submission: WebFormSubmission): void {
    this.retryingId.set(submission.id);
    this.#service.retry(submission.id).subscribe({
      next: (ticketId) => {
        this.retryingId.set(null);
        if (ticketId) {
          this.#toast.success('webForms.admin.retrySucceeded');
        } else {
          this.#toast.error('webForms.admin.retryFailed');
        }
        this.load();
      },
      error: () => this.retryingId.set(null),
    });
  }

  statusClasses(status: string): string {
    if (status === 'Processed') return 'chip border-emerald-200 bg-emerald-50 text-emerald-700';
    if (status === 'Failed') return 'chip border-rose-200 bg-rose-50 text-rose-700';
    return 'chip border-slate-200 bg-slate-100 text-slate-600';
  }
}
