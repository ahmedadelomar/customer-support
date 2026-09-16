import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import type { Observable } from 'rxjs';
import { ToastService } from '../../../../../core/services/toast.service';
import { PageHeaderComponent } from '../../../../../shared/ui/page-header/page-header.component';
import { BusinessCalendarsService } from '../../data-access/business-calendars.service';
import type { BusinessHour, Holiday } from '../../data-access/interfaces/business-calendar.interface';

/** .NET `DayOfWeek`: Sunday = 0 ... Saturday = 6 — the order the Saudi working week is listed in. */
const DAYS = [
  { value: 0, labelKey: 'sla.calendars.day.sunday' },
  { value: 1, labelKey: 'sla.calendars.day.monday' },
  { value: 2, labelKey: 'sla.calendars.day.tuesday' },
  { value: 3, labelKey: 'sla.calendars.day.wednesday' },
  { value: 4, labelKey: 'sla.calendars.day.thursday' },
  { value: 5, labelKey: 'sla.calendars.day.friday' },
  { value: 6, labelKey: 'sla.calendars.day.saturday' },
];

const COMMON_TIME_ZONES = [
  'Asia/Riyadh', 'Asia/Dubai', 'Asia/Kuwait', 'Asia/Qatar', 'Asia/Bahrain',
  'Africa/Cairo', 'Europe/London', 'Europe/Istanbul', 'America/New_York', 'UTC',
];

/** Create/edit a working-hours calendar: time zone, 24/7 toggle, weekly windows and holidays. */
@Component({
  selector: 'app-calendar-form',
  imports: [FormsModule, RouterLink, TranslatePipe, PageHeaderComponent],
  templateUrl: './calendar-form.page.html',
})
export class CalendarFormPage {
  readonly #route = inject(ActivatedRoute);
  readonly #router = inject(Router);
  readonly #service = inject(BusinessCalendarsService);
  readonly #toast = inject(ToastService);

  readonly calendarId = this.#route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.calendarId;

  readonly loading = signal(!!this.calendarId);
  readonly saving = signal(false);

  readonly nameEn = signal('');
  readonly nameAr = signal('');
  readonly timeZoneId = signal('Asia/Riyadh');
  readonly isTwentyFourSeven = signal(false);
  readonly isDefault = signal(false);
  readonly hours = signal<BusinessHour[]>([]);
  readonly holidays = signal<Holiday[]>([]);

  readonly days = DAYS;
  readonly timeZones = COMMON_TIME_ZONES;

  constructor() {
    if (this.calendarId) {
      this.#service.list().subscribe({
        next: (calendars) => {
          const calendar = calendars.find((c) => c.id === this.calendarId);
          if (!calendar) {
            this.#toast.error('common.notFound');
            void this.#router.navigate(['/admin/sla/calendars']);
            return;
          }

          this.nameEn.set(calendar.nameEn);
          this.nameAr.set(calendar.nameAr);
          this.timeZoneId.set(calendar.timeZoneId);
          this.isTwentyFourSeven.set(calendar.isTwentyFourSeven);
          this.isDefault.set(calendar.isDefault);
          this.hours.set(calendar.businessHours);
          this.holidays.set(calendar.holidays);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    }
  }

  dayLabelKey(dayOfWeek: number): string {
    return this.days.find((d) => d.value === dayOfWeek)?.labelKey ?? '';
  }

  addWindow(): void {
    this.hours.update((rows) => [...rows, { dayOfWeek: 0, startTime: '08:00:00', endTime: '17:00:00' }]);
  }

  updateWindow(index: number, patch: Partial<BusinessHour>): void {
    this.hours.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  removeWindow(index: number): void {
    this.hours.update((rows) => rows.filter((_, i) => i !== index));
  }

  addHoliday(): void {
    this.holidays.update((rows) => [
      ...rows,
      { date: new Date().toISOString().slice(0, 10), nameEn: '', nameAr: '', isRecurringAnnually: false },
    ]);
  }

  updateHoliday(index: number, patch: Partial<Holiday>): void {
    this.holidays.update((rows) => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  removeHoliday(index: number): void {
    this.holidays.update((rows) => rows.filter((_, i) => i !== index));
  }

  save(): void {
    if (!this.nameEn().trim() || !this.nameAr().trim()) {
      this.#toast.error('common.fixValidationErrors');
      return;
    }

    if (!this.isTwentyFourSeven() && this.hours().length === 0) {
      this.#toast.error('sla.calendars.needsHoursOrTwentyFourSeven');
      return;
    }

    this.saving.set(true);
    const request = {
      nameEn: this.nameEn(),
      nameAr: this.nameAr(),
      timeZoneId: this.timeZoneId(),
      isTwentyFourSeven: this.isTwentyFourSeven(),
      isDefault: this.isDefault(),
      businessHours: this.hours(),
      holidays: this.holidays(),
    };

    const request$: Observable<string | void> = this.calendarId
      ? this.#service.update(this.calendarId, request)
      : this.#service.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.#toast.success(this.calendarId ? 'common.updated' : 'common.created');
        void this.#router.navigate(['/admin/sla/calendars']);
      },
      error: () => this.saving.set(false),
    });
  }
}
