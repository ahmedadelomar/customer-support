import { Component, type OnChanges, computed, inject, signal, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from '../../../../core/services/language.service';
import { EmptyStateComponent } from '../../../../shared/ui/empty-state/empty-state.component';
import type { Attachment } from '../../../../shared/ui/file-upload/attachment.models';
import { AttachmentsService } from '../../../../shared/ui/file-upload/attachments.service';
import { FileUploadComponent } from '../../../../shared/ui/file-upload/file-upload.component';
import { PaginationComponent } from '../../../../shared/ui/pagination/pagination.component';
import { relativeTime } from '../../../../shared/utils/relative-time';
import type { CustomerNote } from '../customer.models';
import { CustomerNotesService } from './customer-notes.service';

/**
 * The Notes tab: internal notes with attachments, backed by the shared attachment store — see
 * `.squad/plans/customer-management/13-story-notes-attachments-CS-104-notes-attachments.md`.
 *
 * A new note is created from just its body, then immediately shown with its upload widget
 * expanded — attachments need a real note id to attach to, so "compose text and files together"
 * becomes "save the text, then attach files to what you just saved" in one continuous flow rather
 * than a separate step the agent has to notice.
 */
@Component({
  selector: 'app-notes-panel',
  imports: [FormsModule, TranslatePipe, EmptyStateComponent, FileUploadComponent, PaginationComponent],
  templateUrl: './notes-panel.component.html',
})
export class NotesPanelComponent implements OnChanges {
  readonly #service = inject(CustomerNotesService);
  readonly #attachments = inject(AttachmentsService);
  readonly #language = inject(LanguageService);
  readonly #translate = inject(TranslateService);

  readonly customerId = input.required<string>();

  readonly notes = signal<CustomerNote[]>([]);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);

  readonly composerBody = signal('');
  readonly composerInternal = signal(true);
  readonly creating = signal(false);

  readonly editingNoteId = signal<string | null>(null);
  readonly editingBody = signal('');

  /** The note whose upload widget is expanded — set right after creating a note. */
  readonly activeUploadNoteId = signal<string | null>(null);

  readonly locale = computed(() => this.#language.locale());

  ngOnChanges(): void {
    this.page.set(1);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list(this.customerId(), this.page(), this.pageSize).subscribe({
      next: (result) => {
        this.notes.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onPageChange(page: number): void {
    this.page.set(page);
    this.load();
  }

  relativeTime(iso: string): string {
    return relativeTime(iso, this.locale());
  }

  authorName(note: CustomerNote): string | undefined {
    if (!note.authorNameEn && !note.authorNameAr) return undefined;
    return this.#language.pick({ en: note.authorNameEn ?? '', ar: note.authorNameAr ?? '' });
  }

  createNote(): void {
    const body = this.composerBody().trim();
    if (!body) return;

    this.creating.set(true);
    this.#service.create(this.customerId(), { body, isInternal: this.composerInternal() }).subscribe({
      next: (note) => {
        this.creating.set(false);
        this.composerBody.set('');
        this.notes.update((list) => [note, ...list]);
        this.totalCount.update((c) => c + 1);
        // Offer to attach files to the note that just landed, right where it appears.
        this.activeUploadNoteId.set(note.id);
      },
      error: () => this.creating.set(false),
    });
  }

  onAttachmentUploaded(note: CustomerNote, attachment: Attachment): void {
    this.notes.update((list) =>
      list.map((n) => (n.id === note.id ? { ...n, attachments: [...n.attachments, attachment] } : n)),
    );
  }

  startAttaching(noteId: string): void {
    this.activeUploadNoteId.set(noteId);
  }

  finishAttaching(): void {
    this.activeUploadNoteId.set(null);
  }

  startEdit(note: CustomerNote): void {
    this.editingNoteId.set(note.id);
    this.editingBody.set(note.body);
  }

  cancelEdit(): void {
    this.editingNoteId.set(null);
  }

  saveEdit(note: CustomerNote): void {
    const body = this.editingBody().trim();
    if (!body) return;

    this.#service.update(this.customerId(), note.id, { body }).subscribe({
      next: () => {
        this.notes.update((list) => list.map((n) => (n.id === note.id ? { ...n, body } : n)));
        this.editingNoteId.set(null);
      },
    });
  }

  deleteNote(note: CustomerNote): void {
    if (!confirm(this.#translate.instant('customers.notes.deleteConfirm'))) {
      return;
    }

    this.#service.delete(this.customerId(), note.id).subscribe({
      next: () => {
        this.notes.update((list) => list.filter((n) => n.id !== note.id));
        this.totalCount.update((c) => c - 1);
      },
    });
  }

  togglePin(note: CustomerNote): void {
    this.#service.togglePin(this.customerId(), note.id).subscribe({
      // Pin order is decided server-side; simplest correct move is to re-fetch the current page
      // rather than duplicate that ordering logic on the client.
      next: () => this.load(),
    });
  }

  downloadAttachment(attachment: Attachment): void {
    this.#attachments.downloadBlob(attachment.id).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = attachment.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  deleteAttachment(note: CustomerNote, attachment: Attachment): void {
    this.#attachments.delete(attachment.id).subscribe({
      next: () => {
        this.notes.update((list) =>
          list.map((n) =>
            n.id === note.id ? { ...n, attachments: n.attachments.filter((a) => a.id !== attachment.id) } : n,
          ),
        );
      },
    });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }
}
