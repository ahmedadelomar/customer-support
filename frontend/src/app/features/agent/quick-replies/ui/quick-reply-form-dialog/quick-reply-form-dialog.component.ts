import { Component, ElementRef, type OnChanges, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { AuthService } from '../../../../../core/auth/auth.service';
import { PERMISSIONS } from '../../../../../core/permissions';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { ModalComponent } from '../../../../../shared/ui/modal/modal.component';
import { QuickRepliesService } from '../../data-access/quick-replies.service';
import type {
  QuickReply,
  QuickReplyScope,
  SampleTicket,
} from '../../data-access/interfaces/quick-reply.interface';
import { QUICK_REPLY_TOKENS } from '../../data-access/interfaces/quick-reply.interface';

type BodyField = 'bodyEn' | 'bodyAr';

/**
 * The quick reply composer/editor form (Agent Dashboard / Quick replies): both language bodies side
 * by side, a token palette that inserts at the cursor, scope/shortcut/category/channel, and a live
 * preview rendered server-side against a sample ticket — never a client-side re-implementation of
 * the resolver, since the story's own rule is that resolution lives in exactly one place.
 */
@Component({
  selector: 'app-quick-reply-form-dialog',
  imports: [FormsModule, TranslatePipe, ModalComponent],
  templateUrl: './quick-reply-form-dialog.component.html',
})
export class QuickReplyFormDialogComponent implements OnChanges {
  readonly #service = inject(QuickRepliesService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #auth = inject(AuthService);

  readonly open = input(false);
  readonly quickReply = input<QuickReply | null>(null);

  readonly saved = output<void>();
  readonly closed = output<void>();

  readonly tokens = QUICK_REPLY_TOKENS;
  readonly canUseGlobal = computed(() => this.#auth.hasPermission(PERMISSIONS.workspace.manageGlobalQuickReplies));

  readonly scope = signal<QuickReplyScope>('Personal');
  readonly teamId = signal('');
  readonly shortcut = signal('');
  readonly titleEn = signal('');
  readonly titleAr = signal('');
  readonly bodyEn = signal('');
  readonly bodyAr = signal('');
  readonly categoryId = signal('');
  readonly channelId = signal('');
  readonly isActive = signal(true);

  readonly categories = signal<{ id: string; nameEn: string; nameAr: string }[]>([]);
  readonly channels = signal<{ id: string; nameEn: string; nameAr: string }[]>([]);
  readonly sampleTickets = signal<SampleTicket[]>([]);
  readonly previewTicketId = signal('');
  readonly previewResult = signal<{ body: string; unresolvedTokens: string[] } | null>(null);
  readonly previewing = signal(false);

  readonly saving = signal(false);
  readonly errors = signal<Record<string, string[]>>({});

  // `viewChild` cannot be declared on a native `#private` field.
  private readonly bodyEnInput = viewChild<ElementRef<HTMLTextAreaElement>>('bodyEnInput');
  private readonly bodyArInput = viewChild<ElementRef<HTMLTextAreaElement>>('bodyArInput');

  ngOnChanges(): void {
    if (!this.open()) return;

    this.errors.set({});
    this.previewResult.set(null);
    this.#service.lookups().subscribe((lookups) => {
      this.categories.set(lookups.categories);
      this.channels.set(lookups.channels);
    });
    this.#service.sampleTickets().subscribe((tickets) => {
      this.sampleTickets.set(tickets);
      if (tickets.length && !this.previewTicketId()) this.previewTicketId.set(tickets[0].id);
    });

    const existing = this.quickReply();
    if (existing) {
      this.scope.set(existing.scope);
      this.teamId.set(existing.teamId ?? '');
      this.shortcut.set(existing.shortcut ?? '');
      this.titleEn.set(existing.titleEn);
      this.titleAr.set(existing.titleAr);
      this.bodyEn.set(existing.bodyEn);
      this.bodyAr.set(existing.bodyAr);
      this.categoryId.set(existing.categoryId ?? '');
      this.channelId.set(existing.channelId ?? '');
      this.isActive.set(existing.isActive);
    } else {
      this.scope.set('Personal');
      this.teamId.set('');
      this.shortcut.set('');
      this.titleEn.set('');
      this.titleAr.set('');
      this.bodyEn.set('');
      this.bodyAr.set('');
      this.categoryId.set('');
      this.channelId.set('');
      this.isActive.set(true);
    }
  }

  localizedName(item: { nameEn: string; nameAr: string }): string {
    return this.#language.pick({ en: item.nameEn, ar: item.nameAr });
  }

  insertToken(field: BodyField, token: string): void {
    const textarea = (field === 'bodyEn' ? this.bodyEnInput() : this.bodyArInput())?.nativeElement;
    const value = field === 'bodyEn' ? this.bodyEn() : this.bodyAr();
    const placeholder = `{{${token}}}`;

    if (!textarea) {
      this.#setBody(field, value + placeholder);
      return;
    }

    const start = textarea.selectionStart ?? value.length;
    const end = textarea.selectionEnd ?? value.length;
    const next = value.slice(0, start) + placeholder + value.slice(end);
    this.#setBody(field, next);

    queueMicrotask(() => {
      const caret = start + placeholder.length;
      textarea.focus();
      textarea.setSelectionRange(caret, caret);
    });
  }

  close(): void {
    this.closed.emit();
  }

  runPreview(): void {
    const ticketId = this.previewTicketId();
    if (!ticketId) return;

    this.previewing.set(true);
    this.#service.preview({ ticketId, bodyEn: this.bodyEn(), bodyAr: this.bodyAr() }).subscribe({
      next: (result) => {
        this.previewing.set(false);
        this.previewResult.set(result);
      },
      error: () => this.previewing.set(false),
    });
  }

  save(): void {
    this.saving.set(true);
    this.errors.set({});

    const existing = this.quickReply();
    const payload = {
      scope: this.scope(),
      teamId: this.scope() === 'Team' ? this.teamId() || undefined : undefined,
      shortcut: this.shortcut().trim() || undefined,
      titleEn: this.titleEn(),
      titleAr: this.titleAr(),
      bodyEn: this.bodyEn(),
      bodyAr: this.bodyAr(),
      categoryId: this.categoryId() || undefined,
      channelId: this.channelId() || undefined,
    };

    const request$: Observable<string | void> = existing
      ? this.#service.update({ ...payload, id: existing.id, isActive: this.isActive() })
      : this.#service.create(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(existing ? 'quickReplies.updated' : 'quickReplies.created');
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.errors.set(this.#extractFieldErrors(error));
      },
    });
  }

  #setBody(field: BodyField, value: string): void {
    if (field === 'bodyEn') this.bodyEn.set(value);
    else this.bodyAr.set(value);
  }

  #extractFieldErrors(error: unknown): Record<string, string[]> {
    if (error && typeof error === 'object' && 'error' in error) {
      const problem = (error as { error?: { errors?: Record<string, string[]>; detail?: string } }).error;
      if (problem?.errors) return problem.errors;
      if (problem?.detail) return { shortcut: [problem.detail] };
    }
    return {};
  }
}
