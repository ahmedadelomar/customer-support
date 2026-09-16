import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { WebFormsService } from '../../data-access/web-forms.service';
import type { WebFormDefinition } from '../../data-access/interfaces/web-form-admin.interface';

/** Web form administration (Communication Channels / Web forms, CS-305): every public form, its URL and embed snippet. */
@Component({
  selector: 'app-web-form-list',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './web-form-list.page.html',
})
export class WebFormListPage {
  readonly #service = inject(WebFormsService);
  readonly #toast = inject(ToastService);

  readonly forms = signal<WebFormDefinition[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (forms) => {
        this.forms.set(forms);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  publicUrl(form: WebFormDefinition): string {
    return `${window.location.origin}/forms/${form.key}`;
  }

  embedSnippet(form: WebFormDefinition): string {
    return `<iframe src="${this.publicUrl(form)}" style="width:100%;height:640px;border:none;"></iframe>`;
  }

  async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.#toast.success('common.copied');
    } catch {
      this.#toast.error('common.copyFailed');
    }
  }
}
