import { Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../../../../core/auth/auth.service';
import { CollaborationHubService } from '../../data-access/collaboration-hub.service';

/**
 * Viewer avatars for the ticket header (Agent Dashboard / Team collaboration). Purely a reader of
 * `CollaborationHubService.presence` — `ticket-detail.page` owns joining/leaving the hub group so
 * this component and the composer's collision warning never race each other over who joins first.
 */
@Component({
  selector: 'app-presence-bar',
  imports: [TranslatePipe],
  templateUrl: './presence-bar.component.html',
})
export class PresenceBarComponent {
  readonly #hub = inject(CollaborationHubService);
  readonly #auth = inject(AuthService);

  /** Unused directly — kept so the component only renders once bound to a specific ticket's context. */
  readonly ticketId = input.required<string>();

  readonly viewers = computed(() =>
    this.#hub.presence().filter((p) => p.userId !== this.#auth.user()?.id),
  );
}
