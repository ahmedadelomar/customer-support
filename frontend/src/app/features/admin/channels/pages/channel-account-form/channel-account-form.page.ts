import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import type { Department } from '../../../organization/data-access/organization.service';
import { OrganizationService } from '../../../organization/data-access/organization.service';
import { TicketCategoriesService } from '../../../categories/data-access/ticket-categories.service';
import type { TicketCategoryAdmin } from '../../../categories/data-access/interfaces/ticket-category.interface';
import { TicketPrioritiesService } from '../../../priorities/data-access/ticket-priorities.service';
import type { TicketPriorityAdmin } from '../../../priorities/data-access/interfaces/ticket-priority.interface';
import { ChannelAccountsService } from '../../data-access/channel-accounts.service';
import type { ChannelLookup } from '../../data-access/interfaces/channel-account.interface';

/** Create/edit a channel account: identifier, routing defaults, bilingual signature/auto-reply, and the write-only webhook secret. */
@Component({
  selector: 'app-channel-account-form',
  imports: [FormsModule, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './channel-account-form.page.html',
})
export class ChannelAccountFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #service = inject(ChannelAccountsService);
  readonly #categories = inject(TicketCategoriesService);
  readonly #priorities = inject(TicketPrioritiesService);
  readonly #organization = inject(OrganizationService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly accountId = this.#route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.accountId;

  readonly loading = signal(this.isEdit);
  readonly saving = signal(false);

  readonly channelId = signal('');
  readonly name = signal('');
  readonly identifier = signal('');
  readonly defaultDepartmentId = signal('');
  readonly defaultCategoryId = signal('');
  readonly defaultPriorityId = signal('');
  readonly signatureEn = signal('');
  readonly signatureAr = signal('');
  readonly autoReplyBodyEn = signal('');
  readonly autoReplyBodyAr = signal('');
  readonly sendAutoReply = signal(true);
  readonly settingsJson = signal('');
  readonly isActive = signal(true);
  readonly webhookSecret = signal('');
  readonly hasWebhookSecret = signal(false);

  readonly channels = signal<ChannelLookup[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly categories = signal<TicketCategoryAdmin[]>([]);
  readonly priorities = signal<TicketPriorityAdmin[]>([]);

  constructor() {
    this.#service.channels().subscribe((channels) => {
      this.channels.set(channels);
      if (!this.isEdit && channels.length > 0) {
        this.channelId.set(channels[0].id);
      }
    });
    this.#organization.departments().subscribe((departments) => this.departments.set(departments));
    this.#categories.list().subscribe((categories) => this.categories.set(categories));
    this.#priorities.list().subscribe((priorities) => this.priorities.set(priorities));

    if (this.accountId) {
      this.#service.list().subscribe({
        next: (accounts) => {
          const account = accounts.find((a) => a.id === this.accountId);
          if (!account) {
            this.#toast.error('common.notFound');
            void this.#router.navigate(['/admin/channels']);
            return;
          }

          this.channelId.set(account.channelId);
          this.name.set(account.name);
          this.identifier.set(account.identifier);
          this.defaultDepartmentId.set(account.defaultDepartmentId ?? '');
          this.defaultCategoryId.set(account.defaultCategoryId ?? '');
          this.defaultPriorityId.set(account.defaultPriorityId ?? '');
          this.signatureEn.set(account.signatureEn);
          this.signatureAr.set(account.signatureAr);
          this.autoReplyBodyEn.set(account.autoReplyBodyEn);
          this.autoReplyBodyAr.set(account.autoReplyBodyAr);
          this.sendAutoReply.set(account.sendAutoReply);
          this.settingsJson.set(account.settingsJson ?? '');
          this.isActive.set(account.isActive);
          this.hasWebhookSecret.set(account.hasWebhookSecret);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  channelName(channel: ChannelLookup): string {
    return this.#language.pick({ en: channel.nameEn, ar: channel.nameAr });
  }

  departmentName(department: Department): string {
    return this.#language.pick({ en: department.nameEn, ar: department.nameAr });
  }

  categoryName(category: TicketCategoryAdmin): string {
    return this.#language.pick({ en: category.nameEn, ar: category.nameAr });
  }

  priorityName(priority: TicketPriorityAdmin): string {
    return this.#language.pick({ en: priority.nameEn, ar: priority.nameAr });
  }

  save(): void {
    if (!this.channelId() || !this.name().trim() || !this.identifier().trim()) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    this.saving.set(true);
    const request = {
      channelId: this.channelId(),
      name: this.name(),
      identifier: this.identifier(),
      defaultDepartmentId: this.defaultDepartmentId() || null,
      defaultCategoryId: this.defaultCategoryId() || null,
      defaultPriorityId: this.defaultPriorityId() || null,
      signatureEn: this.signatureEn(),
      signatureAr: this.signatureAr(),
      autoReplyBodyEn: this.autoReplyBodyEn(),
      autoReplyBodyAr: this.autoReplyBodyAr(),
      sendAutoReply: this.sendAutoReply(),
      settingsJson: this.settingsJson() || null,
      isActive: this.isActive(),
      // Omitted (undefined) leaves the secret unchanged on edit; a blank field means "don't touch it"
      // rather than accidentally clearing a working secret by leaving the field empty when reopening the form.
      ...(this.webhookSecret() ? { webhookSecret: this.webhookSecret() } : {}),
    };

    const request$: Observable<string | void> = this.accountId
      ? this.#service.update(this.accountId, request)
      : this.#service.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.accountId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/channels']);
      },
      error: () => this.saving.set(false),
    });
  }
}
