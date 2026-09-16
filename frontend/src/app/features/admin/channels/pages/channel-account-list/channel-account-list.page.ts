import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { ChannelAccountsService } from '../../data-access/channel-accounts.service';
import type { ChannelAccount } from '../../data-access/interfaces/channel-account.interface';

/** Channel account administration (Communication Channels / Email channel — every inbound channel shares this one screen). */
@Component({
  selector: 'app-channel-account-list',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './channel-account-list.page.html',
})
export class ChannelAccountListPage {
  readonly #service = inject(ChannelAccountsService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  readonly accounts = signal<ChannelAccount[]>([]);
  readonly loading = signal(true);
  readonly testingId = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (accounts) => {
        this.accounts.set(accounts);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  edit(account: ChannelAccount): void {
    void this.#router.navigate(['/admin/channels', account.id]);
  }

  test(account: ChannelAccount): void {
    this.testingId.set(account.id);
    this.#service.test(account.id).subscribe({
      next: (result) => {
        this.testingId.set(null);
        if (result.success) {
          this.#toast.success('channels.testSuccess');
        } else {
          this.#toast.error(result.error ?? 'channels.testFailed');
        }
        this.load();
      },
      error: () => this.testingId.set(null),
    });
  }

  delete(account: ChannelAccount): void {
    if (!confirm(`Delete "${account.name}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(account.id).subscribe({
      next: () => {
        this.#toast.success('common.deleted');
        this.load();
      },
    });
  }
}
