// Calendar days as "YYYY-MM-DD" in the viewer's time zone, the format of <input type="date">.
export function localDay(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

export function today(): string {
  return localDay(new Date());
}

// Matches the server's earliest allowed watch date.
export const EARLIEST_WATCH_DAY = "1888-01-01";

// Every date on the site is formatted here, in one locale, so a date reads the same on every page.
// Moments (when something was written or happened) are shown in the viewer's time zone. Calendar days
// (diary days, stored at 12:00 UTC, and plain dates such as birthdays) are shown in UTC, as the server
// and lib/stats.ts count them, so a day never shifts with the viewer's time zone.
type DateInput = string | number | Date;

function formatter(options: Intl.DateTimeFormatOptions) {
  const format = new Intl.DateTimeFormat("en", options);
  return (value: DateInput) => format.format(new Date(value));
}

const UTC = { timeZone: "UTC" } as const;

// Moments.
export const formatDate = formatter({ day: "numeric", month: "short", year: "numeric" });
export const formatDateLong = formatter({ day: "numeric", month: "long", year: "numeric" });
export const formatDateTime = formatter({ dateStyle: "long", timeStyle: "short" });

// Calendar days.
export const formatDay = formatter({ day: "numeric", month: "short", year: "numeric", ...UTC });
export const formatDayLong = formatter({ day: "numeric", month: "long", year: "numeric", ...UTC });
export const formatDayFull = formatter({ dateStyle: "full", ...UTC });
export const formatDayOfMonth = formatter({ day: "numeric", ...UTC });
export const formatWeekday = formatter({ weekday: "long", ...UTC });
export const formatWeekdayShort = formatter({ weekday: "short", ...UTC });
export const formatMonth = formatter({ month: "long", year: "numeric", ...UTC });
export const formatMonthShort = formatter({ month: "short", ...UTC });
