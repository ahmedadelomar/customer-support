import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { QuickRepliesService } from '../../data-access/quick-replies.service';
import type { QuickReply, QuickReplyScope } from '../../data-access/interfaces/quick-reply.interface';
import { QuickReplyFormDialogComponent } from '../../ui/quick-reply-form-dialog/quick-reply-form-dialog.component';

/** The quick-reply management screen (Agent Dashboard / Quick replies). */
@Component({
  selector: 'app-quick-reply-list',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent, EmptyStateComponent, QuickReplyFormDialogComponent],
  templateUrl: './quick-reply-list.page.html',
})
export class QuickReplyListPage {
  readonly #service = inject(QuickRepliesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);

  readonly items = signal<QuickReply[]>([]);
  readonly loading = signal(true);
  readonly scopeFilter = signal<QuickReplyScope | ''>('');

  readonly formOpen = signal(false);
  readonly editing = signal<QuickReply | null>(null);

  readonly scopeOptions: { value: QuickReplyScope | ''; labelKey: string }[] = [
    { value: '', labelKey: 'quickReplies.filters.allScopes' },
    { value: 'Personal', labelKey: 'quickReplies.scope.personal' },
    { value: 'Team', labelKey: 'quickReplies.scope.team' },
    { value: 'Global', labelKey: 'quickReplies.scope.global' },
  ];

  readonly lang = computed(() => this.#language.current());

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const scope = this.scopeFilter();

    this.#service.list({ scope: scope || undefined, includeInactive: true }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFilterChange(): void {
    this.load();
  }

  title(item: QuickReply): string {
    return this.lang() === 'ar' ? item.titleAr : item.titleEn;
  }

  bodyPreview(item: QuickReply): string {
    const body = this.lang() === 'ar' ? item.bodyAr : item.bodyEn;
    return body.length > 80 ? `${body.slice(0, 80)}…` : body;
  }

  openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  openEdit(item: QuickReply): void {
    this.editing.set(item);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  onSaved(): void {
    this.formOpen.set(false);
    this.load();
  }

  delete(item: QuickReply): void {
    this.#service.delete(item.id).subscribe({
      next: () => {
        this.#toast.success('quickReplies.deleted');
        this.load();
      },
    });
  }
}
