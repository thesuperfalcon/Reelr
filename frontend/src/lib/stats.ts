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
