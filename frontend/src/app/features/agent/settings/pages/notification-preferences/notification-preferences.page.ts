import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { NotificationsService } from '../../../../../layout/notifications/data-access/notifications.service';
import type { NotificationArea, NotificationPreference } from '../../../../../layout/notifications/data-access/interfaces/notification.interface';

const AREA_LABEL_KEYS: Record<NotificationArea, string> = {
  0: 'notifications.preferences.area.tickets',
  1: 'notifications.preferences.area.sla',
  2: 'notifications.preferences.area.tasks',
  3: 'notifications.preferences.area.collaboration',
};

const AREA_ORDER: NotificationArea[] = [0, 1, 2, 3];

interface AreaGroup {
  area: NotificationArea;
  labelKey: string;
  rows: NotificationPreference[];
}

/**
 * The notification channel matrix (SLA and Automation / Alerts and notifications), grouped by area
 * so it reads as sections rather than a flat list of rows. Rows with no saved preference show the
 * system default visibly marked, per that story's own rule — a user should see what will happen
 * before they change anything. Quiet hours are exposed once, applied across every row on save,
 * rather than per event type: the data model allows finer control, but nobody wants to reason about
 * "warnings respect quiet hours but breaches don't" on top of the Critical-bypass rule that already exists.
 */
@Component({
  selector: 'app-notification-preferences',
  imports: [FormsModule, TranslatePipe, PageHeaderComponent],
  templateUrl: './notification-preferences.page.html',
})
export class NotificationPreferencesPage {
  readonly #service = inject(NotificationsService);
  readonly #toast = inject(ToastService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly rows = signal<NotificationPreference[]>([]);
  readonly quietHoursStart = signal('');
  readonly quietHoursEnd = signal('');

  readonly groups = computed<AreaGroup[]>(() =>
    AREA_ORDER.map((area) => ({
      area,
      labelKey: AREA_LABEL_KEYS[area],
      rows: this.rows().filter((r) => r.area === area),
    })).filter((group) => group.rows.length > 0),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.preferences().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        const withQuietHours = rows.find((r) => r.quietHoursStart && r.quietHoursEnd);
        this.quietHoursStart.set(withQuietHours?.quietHoursStart?.slice(0, 5) ?? '');
        this.quietHoursEnd.set(withQuietHours?.quietHoursEnd?.slice(0, 5) ?? '');
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggle(eventType: string, channel: 'viaInApp' | 'viaEmail' | 'viaSms' | 'viaPush', value: boolean): void {
    this.rows.update((rows) =>
      rows.map((r) => (r.eventType === eventType ? { ...r, [channel]: value, isUsingDefault: false } : r)),
    );
  }

  save(): void {
    this.saving.set(true);
    const start = this.quietHoursStart() ? `${this.quietHoursStart()}:00` : null;
    const end = this.quietHoursEnd() ? `${this.quietHoursEnd()}:00` : null;

    const request = this.rows().map((r) => ({
      eventType: r.eventType,
      viaInApp: r.viaInApp,
      viaEmail: r.viaEmail,
      viaSms: r.viaSms,
      viaPush: r.viaPush,
      quietHoursStart: start,
      quietHoursEnd: end,
    }));

    this.#service.updatePreferences(request).subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success('common.updated');
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }
}
