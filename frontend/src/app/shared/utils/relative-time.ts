/**
 * Locale-correct relative time via `Intl.RelativeTimeFormat` rather than a hand-rolled
 * "N hours ago" string — it gets Arabic pluralisation (which has distinct dual/plural forms
 * English doesn't) right for free. Shared by every feature that shows a timestamp this way
 * (the interaction timeline, customer notes, and — eventually — the ticket conversation).
 */
export function relativeTime(iso: string, locale: string): string {
  const diffSeconds = Math.round((new Date(iso).getTime() - Date.now()) / 1000);
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });

  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['week', 604_800],
    ['day', 86_400],
    ['hour', 3_600],
    ['minute', 60],
  ];

  for (const [unit, secondsInUnit] of units) {
    if (Math.abs(diffSeconds) >= secondsInUnit) {
      return formatter.format(Math.round(diffSeconds / secondsInUnit), unit);
    }
  }

  return formatter.format(diffSeconds, 'second');
}
