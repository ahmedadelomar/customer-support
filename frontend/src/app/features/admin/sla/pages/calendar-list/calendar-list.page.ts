import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService } from '../../../../../core/services/language.service';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { BusinessCalendarsService } from '../../data-access/business-calendars.service';
import type { BusinessCalendar } from '../../data-access/interfaces/business-calendar.interface';

/** Business calendar administration (SLA and Automation / Response and resolution targets). */
@Component({
  selector: 'app-calendar-list',
  imports: [RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './calendar-list.page.html',
})
export class CalendarListPage {
  readonly #service = inject(BusinessCalendarsService);
  readonly #language = inject(LanguageService);
  readonly #toast = inject(ToastService);
  readonly #router = inject(Router);

  readonly calendars = signal<BusinessCalendar[]>([]);
  readonly loading = signal(true);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.#service.list().subscribe({
      next: (calendars) => {
        this.calendars.set(calendars);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  name(calendar: BusinessCalendar): string {
    return this.#language.pick({ en: calendar.nameEn, ar: calendar.nameAr });
  }

  edit(calendar: BusinessCalendar): void {
    void this.#router.navigate(['/admin/sla/calendars', calendar.id]);
  }

  delete(calendar: BusinessCalendar): void {
    if (!confirm(`Delete "${this.name(calendar)}"? This cannot be undone.`)) {
      return;
    }

    this.#service.delete(calendar.id).subscribe({
      next: () => {
        this.#toast.success('common.deleted');
        this.load();
      },
    });
  }
}
