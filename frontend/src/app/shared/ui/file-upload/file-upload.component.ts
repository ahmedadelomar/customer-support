import { HttpEventType } from '@angular/common/http';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AttachmentsService } from './attachments.service';
import type { Attachment, AttachmentPolicy, UploadItem } from './attachment.models';

let nextItemId = 1;

/**
 * Reusable drag-and-drop plus click-to-browse upload control. Any feature attaching files to a
 * record uses this — ticket replies and KB authoring are the next intended callers, which is why
 * it lives under `shared/ui/` and knows nothing about notes specifically.
 */
@Component({
  selector: 'app-file-upload',
  imports: [TranslatePipe],
  templateUrl: './file-upload.component.html',
})
export class FileUploadComponent implements OnChanges {
  readonly #service = inject(AttachmentsService);
  readonly #translate = inject(TranslateService);

  readonly ownerType = input.required<string>();
  readonly ownerId = input.required<string>();

  /** Emitted once per file as soon as its upload succeeds, so the parent can associate it immediately. */
  readonly uploaded = output<Attachment>();

  readonly items = signal<UploadItem[]>([]);
  readonly dragOver = signal(false);
  readonly policy = signal<AttachmentPolicy | null>(null);

  readonly hasFailures = computed(() => this.items().some((i) => i.status === 'error'));

  ngOnChanges(): void {
    // Read once per owner — the policy rarely changes within a session, and every instance of
    // this component would otherwise re-fetch it on every render.
    if (!this.policy()) {
      this.#service.getPolicy().subscribe({ next: (policy) => this.policy.set(policy) });
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(true);
  }

  onDragLeave(): void {
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    if (event.dataTransfer?.files.length) {
      this.#handleFiles(event.dataTransfer.files);
    }
  }

  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.#handleFiles(input.files);
    }
    input.value = ''; // allows re-selecting the same file after removing it
  }

  dismiss(itemId: string): void {
    this.items.update((list) => list.filter((i) => i.id !== itemId));
  }

  #handleFiles(fileList: FileList): void {
    const policy = this.policy();

    for (const file of Array.from(fileList)) {
      const validationError = policy ? this.#validate(file, policy) : null;

      const item: UploadItem = {
        id: `upload-${nextItemId++}`,
        file,
        progress: 0,
        status: validationError ? 'error' : 'pending',
        errorMessage: validationError ?? undefined,
      };

      this.items.update((list) => [...list, item]);

      if (!validationError) {
        this.#upload(item);
      }
    }
  }

  /** A fast, local rejection beats waiting on a round trip for a file that was never going to be accepted. */
  #validate(file: File, policy: AttachmentPolicy): string | null {
    const extension = `.${file.name.split('.').pop()?.toLowerCase() ?? ''}`;

    if (!policy.allowedExtensions.includes(extension)) {
      return this.#translate.instant('fileUpload.disallowedExtension', { extension });
    }

    if (file.size > policy.maxBytes) {
      const maxMb = Math.round(policy.maxBytes / (1024 * 1024));
      return this.#translate.instant('fileUpload.tooLarge', { maxMb });
    }

    return null;
  }

  #upload(item: UploadItem): void {
    this.#setStatus(item.id, { status: 'uploading' });

    this.#service.upload(this.ownerType(), this.ownerId(), item.file).subscribe({
      next: (event) => {
        if (AttachmentsService.isUploadProgress(event) && event.total) {
          this.#setStatus(item.id, { progress: Math.round((100 * event.loaded) / event.total) });
        } else if (event.type === HttpEventType.Response && event.body) {
          this.uploaded.emit(event.body as Attachment);
          // The parent now owns this attachment (via its own list) — remove it from this
          // widget's transient in-progress list rather than showing it twice.
          this.dismiss(item.id);
        }
      },
      error: (error: unknown) => {
        const message =
          error && typeof error === 'object' && 'error' in error
            ? ((error.error as { detail?: string })?.detail ?? this.#translate.instant('errors.unexpected'))
            : this.#translate.instant('errors.unexpected');

        this.#setStatus(item.id, { status: 'error', errorMessage: message });
      },
    });
  }

  #setStatus(itemId: string, patch: Partial<UploadItem>): void {
    this.items.update((list) => list.map((i) => (i.id === itemId ? { ...i, ...patch } : i)));
  }
}
