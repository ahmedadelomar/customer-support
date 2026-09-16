export interface BusinessHour {
  /** .NET `DayOfWeek`: Sunday = 0 ... Saturday = 6. */
  dayOfWeek: number;
  /** `HH:mm:ss`. */
  startTime: string;
  endTime: string;
}

export interface Holiday {
  /** `yyyy-MM-dd`. */
  date: string;
  nameEn: string;
  nameAr: string;
  isRecurringAnnually: boolean;
}

export interface BusinessCalendar {
  id: string;
  nameEn: string;
  nameAr: string;
  timeZoneId: string;
  isTwentyFourSeven: boolean;
  isDefault: boolean;
  /** True when an SLA policy references this calendar — refuses deletion. */
  isInUse: boolean;
  businessHours: BusinessHour[];
  holidays: Holiday[];
}

export interface BusinessCalendarRequest {
  nameEn: string;
  nameAr: string;
  timeZoneId: string;
  isTwentyFourSeven: boolean;
  isDefault: boolean;
  businessHours: BusinessHour[];
  holidays: Holiday[];
}
