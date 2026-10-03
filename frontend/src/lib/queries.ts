import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import type {
  DiaryEntry,
  MovieDetails,
  SearchAllResult,
  SearchResult,
  UserProfile,
  UserSummary,
  WatchlistEntry,
} from "./types";

export function useTrendingMovies() {
  return useQuery({
    queryKey: ["movies", "trending"],
    queryFn: () => api<SearchResult>("/api/movie/trending"),
  });
}

export function usePopularMovies() {
  return useQuery({
    queryKey: ["movies", "popular"],
    queryFn: () => api<SearchResult>("/api/movie/popular"),
  });
}

export function useMovieDetails(tmdbId: number) {
  return useQuery({
    queryKey: ["movies", tmdbId, "details"],
    queryFn: () => api<MovieDetails>(`/api/movie/${tmdbId}/details`),
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useSimilarMovies(tmdbId: number) {
  return useQuery({
    queryKey: ["movies", tmdbId, "similar"],
    queryFn: () => api<SearchResult>(`/api/movie/${tmdbId}/recommended`),
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useSearchAll(query: string) {
  const trimmed = query.trim();
  return useQuery({
    queryKey: ["search", trimmed],
    queryFn: () => api<SearchAllResult>(`/api/movie/search/all?query=${encodeURIComponent(trimmed)}`),
    enabled: trimmed.length > 0,
  });
}

export function useUserProfile(userId: number) {
  return useQuery({
    queryKey: ["users", userId],
    queryFn: () => api<UserProfile>(`/api/users/${userId}`),
  });
}

// Watchlist and diary belong to the logged-in user. Logout clears the cache.
export function useWatchlist() {
  return useQuery({
    queryKey: ["me", "watchlist"],
    queryFn: () => api<WatchlistEntry[]>("/api/watchlist"),
  });
}

export function useToggleWatchlist(tmdbId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (onWatchlist: boolean) =>
      api<void>(`/api/watchlist/${tmdbId}`, { method: onWatchlist ? "DELETE" : "POST" }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["me", "watchlist"] }),
  });
}

export function useDiary() {
  return useQuery({
    queryKey: ["me", "diary"],
    queryFn: () => api<DiaryEntry[]>("/api/watched"),
  });
}

export function useFollowList(userId: number, list: "followers" | "following") {
  return useQuery({
    queryKey: ["users", userId, list],
    queryFn: () => api<UserSummary[]>(`/api/users/${userId}/${list}`),
  });
}
