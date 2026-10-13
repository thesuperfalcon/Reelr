import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api } from "./api";
import type { DiaryEntry } from "./types";

export interface DayCount {
  /** "YYYY-MM-DD". */
  date: string;
  count: number;
}

export interface MonthStats {
  year: number;
  /** 1 to 12. */
  month: number;
  count: number;
  averageRating: number | null;
}

/** A member's diary as days, a summary and months. Follows the diary's visibility. */
export interface UserStats {
  /** First and last day covered, both "YYYY-MM-DD" and included. */
  from: string;
  to: string;
  total: number;
  /** Only days with films, oldest first. */
  days: DayCount[];
  longestStreak: number;
  /** Null for a past year. */
  currentStreak: number | null;
  /** 0 is Sunday. Null when nothing was logged. */
  busiestWeekday: number | null;
  months: MonthStats[];
  /** Years with diary entries, newest first. */
  years: number[];
}

/** Null year: the last 365 days. */
export function useUserStats(userId: number, year: number | null) {
  return useQuery({
    queryKey: ["stats", userId, year],
    queryFn: () => api<UserStats>(`/api/users/${userId}/stats${year == null ? "" : `?year=${year}`}`),
    // Keeps the old graph on screen while another year loads, so the layout does not jump.
    placeholderData: keepPreviousData,
  });
}

export function useStatsDay(userId: number, date: string | null) {
  return useQuery({
    queryKey: ["stats", userId, "day", date],
    queryFn: () => api<DiaryEntry[]>(`/api/users/${userId}/stats/days/${date}`),
    enabled: date != null,
  });
}

// Dates from the API are calendar days ("YYYY-MM-DD"). They are handled as UTC midnight, so no time zone can
// move a film to the day before or after.
export const DAY_MS = 24 * 60 * 60 * 1000;
export const toTime = (day: string) => Date.parse(`${day}T00:00:00Z`);
export const toDay = (time: number) => new Date(time).toISOString().slice(0, 10);

export interface Cell {
  day: string;
  count: number;
}

// Weeks as columns from Sunday to Saturday, like GitHub; null pads the days before the first and after the last.
export function buildWeeks(stats: UserStats): (Cell | null)[][] {
  const counts = new Map(stats.days.map((d) => [d.date, d.count]));
  const first = toTime(stats.from);
  const last = toTime(stats.to);
  const start = first - new Date(first).getUTCDay() * DAY_MS;
  const weeks: (Cell | null)[][] = [];

  for (let weekStart = start; weekStart <= last; weekStart += 7 * DAY_MS) {
    const week: (Cell | null)[] = [];
    for (let i = 0; i < 7; i++) {
      const time = weekStart + i * DAY_MS;
      const day = toDay(time);
      week.push(time < first || time > last ? null : { day, count: counts.get(day) ?? 0 });
    }
    weeks.push(week);
  }

  return weeks;
}
