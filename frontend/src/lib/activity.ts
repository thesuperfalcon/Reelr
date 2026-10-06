import type { ActivityItem } from "./types";

const KNOWN_TYPES = new Set(["watched", "reviewed", "listCreated", "listAdded", "watchlistAdded"]);

// The API may send kinds this client does not know yet; those are skipped instead of shown broken.
export function isKnownActivity(item: ActivityItem): boolean {
  return KNOWN_TYPES.has(item.type);
}

const relative = new Intl.RelativeTimeFormat("en", { numeric: "auto" });
export const dayFormat = new Intl.DateTimeFormat("en", { day: "numeric", month: "short", year: "numeric" });
export const fullFormat = new Intl.DateTimeFormat("en", { dateStyle: "long", timeStyle: "short" });

export function timeAgo(date: Date): string {
  const seconds = (date.getTime() - Date.now()) / 1000;
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [
    ["minute", 60],
    ["hour", 60],
    ["day", 24],
    ["week", 7],
  ];

  let value = seconds;
  let unit: Intl.RelativeTimeFormatUnit = "second";
  for (const [next, size] of steps) {
    if (Math.abs(value) < size) {
      break;
    }
    value /= size;
    unit = next;
  }

  if (unit === "week" && Math.abs(value) >= 5) {
    return dayFormat.format(date);
  }

  return Math.abs(value) < 1 && unit === "second" ? "just now" : relative.format(Math.round(value), unit);
}
