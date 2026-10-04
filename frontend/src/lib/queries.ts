import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api } from "./api";
import type {
  DiaryEntry,
  MovieDetails,
  Rating,
  Review,
  SearchAllResult,
  SearchResult,
  Status,
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

// Resolves to null when the user has not rated the film (the API answers 404).
export function useMyRating(tmdbId: number) {
  return useQuery({
    queryKey: ["me", "rating", tmdbId],
    queryFn: async () => {
      try {
        return await api<Rating>(`/api/movies/${tmdbId}/rating`);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }
        throw error;
      }
    },
  });
}

// Resolves to null when the film is not in the user's diary (the API answers 404).
export function useMyStatus(tmdbId: number) {
  return useQuery({
    queryKey: ["me", "status", tmdbId],
    queryFn: async () => {
      try {
        return await api<Status>(`/api/movies/${tmdbId}/status`);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }
        throw error;
      }
    },
  });
}

export interface DiaryEntryInput {
  /** New score, or null to leave the rating as it is. */
  score: number | null;
  liked: boolean | null;
  rewatched: boolean;
  /** Markdown review that creates or replaces the user's review, or null to leave it as it is. */
  review: string | null;
}

// Matches Review.MaxLength on the server.
export const REVIEW_MAX_LENGTH = 5000;

// Saves rating and status from the rating dialog in one request, which logs one new diary entry.
export function useSaveDiaryEntry(tmdbId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: DiaryEntryInput) =>
      api<DiaryEntry>(`/api/movies/${tmdbId}/diary`, {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: (entry) => {
      if (entry.rating !== null) {
        queryClient.setQueryData<Rating>(["me", "rating", tmdbId], { tmdbId, score: entry.rating });
      }
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["me", "rating", tmdbId] }),
        queryClient.invalidateQueries({ queryKey: ["me", "status", tmdbId] }),
        queryClient.invalidateQueries({ queryKey: ["me", "diary"] }),
        // Logging a film removes it from the watchlist on the server.
        queryClient.invalidateQueries({ queryKey: ["me", "watchlist"] }),
        // Reviews show the author's current rating.
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
      ]),
  });
}

export function useDeleteRating(tmdbId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api<void>(`/api/movies/${tmdbId}/rating`, { method: "DELETE" }),
    onSuccess: () => queryClient.setQueryData(["me", "rating", tmdbId], null),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["me", "diary"] }),
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
      ]),
  });
}

export function useMovieReviews(tmdbId: number) {
  return useQuery({
    queryKey: ["reviews", "movie", tmdbId],
    queryFn: () => api<Review[]>(`/api/movies/${tmdbId}/reviews`),
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useUserReviews(userId: number) {
  return useQuery({
    queryKey: ["reviews", "user", userId],
    queryFn: () => api<Review[]>(`/api/users/${userId}/reviews`),
  });
}

// Reviews are written through useSaveDiaryEntry, so writing one also logs the film.
export function useDeleteReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api<void>(`/api/reviews/${id}`, { method: "DELETE" }),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
        queryClient.invalidateQueries({ queryKey: ["me", "diary"] }),
      ]),
  });
}
