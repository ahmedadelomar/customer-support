import { DatePipe } from '@angular/common';
import { Component, type OnChanges, computed, inject, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { AuditService, type AuditLogDetail } from '../../data-access/audit.service';

/**
 * Side panel showing one audit entry's field-level diff.
 *
 * Values are untrusted — they are whatever a user typed into the record that changed — so every one
 * is bound as text. Nothing here ever goes through `innerHTML`.
 */
@Component({
  selector: 'app-audit-detail-panel',
  imports: [DatePipe, TranslatePipe],
  templateUrl: './audit-detail-panel.component.html',
})
export class AuditDetailPanelComponent implements OnChanges {
  readonly #service = inject(AuditService);
  readonly #language = inject(LanguageService);

  readonly entryId = input<string | null>(null);
  readonly closed = output<void>();

  readonly entry = signal<AuditLogDetail | null>(null);
  readonly loading = signal(false);

  readonly locale = computed(() => this.#language.locale());

  ngOnChanges(): void {
    const id = this.entryId();
    if (!id) {
      this.entry.set(null);
      return;
    }

    this.loading.set(true);
    this.#service.getById(id).subscribe({
      next: (entry) => {
        this.entry.set(entry);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  close(): void {
    this.closed.emit();
  }
}
