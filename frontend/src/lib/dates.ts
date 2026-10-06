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
