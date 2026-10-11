import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import type { UserSummary } from "./types";

export type ReportTargetType = "Review" | "Comment" | "List";

export type ReportReason = "Spam" | "Harassment" | "UnmarkedSpoilers" | "Inappropriate" | "Other";

export type ReportOutcome = "Dismissed" | "Removed";

export const REPORT_NOTE_MAX_LENGTH = 500;

export const reportReasons: { value: ReportReason; label: string }[] = [
  { value: "Spam", label: "Spam or advertising" },
  { value: "Harassment", label: "Harassment or hate" },
  { value: "UnmarkedSpoilers", label: "Spoilers without a spoiler warning" },
  { value: "Inappropriate", label: "Other inappropriate content" },
  { value: "Other", label: "Something else" },
];

export interface ReportedItem {
  targetType: ReportTargetType;
  targetId: number;
  /** False when the item was deleted after it was reported. */
  exists: boolean;
  author: UserSummary | null;
  text: string | null;
  context: string | null;
  reviewId: number | null;
  listId: number | null;
  reportCount: number;
  reasons: ReportReason[];
  notes: string[];
  firstReportedAt: string;
}

export function useReport() {
  return useMutation({
    mutationFn: (input: { targetType: ReportTargetType; targetId: number; reason: ReportReason; note: string }) =>
      api<void>("/api/reports", { method: "POST", body: JSON.stringify(input) }),
  });
}

const reportsKey = ["admin", "reports"];

export function useOpenReports() {
  return useQuery({
    queryKey: reportsKey,
    queryFn: () => api<ReportedItem[]>("/api/admin/reports"),
  });
}

export function useResolveReports() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { targetType: ReportTargetType; targetId: number; outcome: ReportOutcome }) =>
      api<void>("/api/admin/reports/resolve", { method: "POST", body: JSON.stringify(input) }),
    onSuccess: () => queryClient.invalidateQueries(),
  });
}

const blockedKey = ["me", "blocked"];

export function useBlockedUsers() {
  return useQuery({
    queryKey: blockedKey,
    queryFn: () => api<UserSummary[]>("/api/settings/blocked"),
  });
}

// Blocking changes follows, feeds, comments and review lists, so every cached page is refreshed.
export function useToggleBlock(userId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (blocked: boolean) => api<void>(`/api/users/${userId}/block`, { method: blocked ? "DELETE" : "PUT" }),
    onSuccess: () => queryClient.invalidateQueries(),
  });
}
