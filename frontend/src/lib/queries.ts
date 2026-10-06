import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, api } from "./api";
import type {
  DiaryEntry,
  MovieDetails,
  MovieList,
  MovieListInput,
  MovieListItem,
  MovieListSummary,
  PersonDetails,
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

// Another user's watchlist. The API answers 404 when their visibility setting hides it from the caller.
export function useUserWatchlist(userId: number) {
  return useQuery({
    queryKey: ["users", userId, "watchlist"],
    queryFn: () => api<WatchlistEntry[]>(`/api/users/${userId}/watchlist`),
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

export function useDiary(enabled = true) {
  return useQuery({
    queryKey: ["me", "diary"],
    queryFn: () => api<DiaryEntry[]>("/api/watched"),
    enabled,
  });
}

export function usePersonDetails(personId: number) {
  return useQuery({
    queryKey: ["people", personId],
    queryFn: () => api<PersonDetails>(`/api/person/${personId}`),
    enabled: Number.isInteger(personId) && personId > 0,
  });
}

export function useUserDiary(userId: number) {
  return useQuery({
    queryKey: ["users", userId, "diary"],
    queryFn: () => api<DiaryEntry[]>(`/api/users/${userId}/diary`),
  });
}

export function useFollowList(userId: number, list: "followers" | "following") {
  return useQuery({
    queryKey: ["users", userId, list],
    queryFn: () => api<UserSummary[]>(`/api/users/${userId}/${list}`),
  });
}

export function useUserSearch(query: string) {
  const trimmed = query.trim();
  return useQuery({
    queryKey: ["users", "search", trimmed],
    queryFn: () => api<UserSummary[]>(`/api/users/search?query=${encodeURIComponent(trimmed)}`),
    enabled: trimmed.length > 0,
  });
}

// Updates the button and follower count at once, then refetches both profiles and their follow lists.
export function useToggleFollow(userId: number, currentUserId: number) {
  const queryClient = useQueryClient();
  const key = ["users", userId];
  return useMutation({
    mutationFn: (following: boolean) =>
      api<void>(`/api/users/${userId}/follow`, { method: following ? "DELETE" : "POST" }),
    onMutate: async (following) => {
      await queryClient.cancelQueries({ queryKey: key, exact: true });
      const previous = queryClient.getQueryData<UserProfile>(key);
      if (previous) {
        queryClient.setQueryData<UserProfile>(key, {
          ...previous,
          isFollowing: !following,
          followerCount: previous.followerCount + (following ? -1 : 1),
        });
      }
      return { previous };
    },
    onError: (_error, _following, context) => {
      if (context?.previous) {
        queryClient.setQueryData(key, context.previous);
      }
    },
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: key }),
        queryClient.invalidateQueries({ queryKey: ["users", currentUserId] }),
      ]),
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
  /** Day watched as "YYYY-MM-DD", or null for now. */
  watchedOn: string | null;
}

export interface DiaryEntryUpdate {
  watchedOn: string;
  rating: number | null;
  liked: boolean | null;
  rewatched: boolean;
}

// Diary edits can change the film's watched status, and deleting an entry deletes the review it logged,
// so they refresh everything about the user and all reviews.
function useInvalidateDiary() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["me"] }),
      queryClient.invalidateQueries({ queryKey: ["users"] }),
      queryClient.invalidateQueries({ queryKey: ["reviews"] }),
    ]);
}

export function useUpdateDiaryEntry() {
  const invalidate = useInvalidateDiary();
  return useMutation({
    mutationFn: ({ id, update }: { id: number; update: DiaryEntryUpdate }) =>
      api<DiaryEntry>(`/api/diary/${id}`, { method: "PUT", body: JSON.stringify(update) }),
    onSettled: invalidate,
  });
}

export function useDeleteDiaryEntry() {
  const invalidate = useInvalidateDiary();
  return useMutation({
    mutationFn: (id: number) => api<void>(`/api/diary/${id}`, { method: "DELETE" }),
    onSettled: invalidate,
  });
}

// Marks the film unwatched and deletes all its diary entries. The rating stays.
export function useRemoveWatched(tmdbId: number) {
  const invalidate = useInvalidateDiary();
  return useMutation({
    mutationFn: () => api<void>(`/api/movies/${tmdbId}/status`, { method: "DELETE" }),
    onSettled: invalidate,
  });
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

export function useReview(reviewId: number) {
  return useQuery({
    queryKey: ["reviews", reviewId],
    queryFn: () => api<Review>(`/api/reviews/${reviewId}`),
    enabled: Number.isInteger(reviewId) && reviewId > 0,
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

// Lists change from several places (movie page, profile, list page), so every write refreshes all of them.
export function useUserLists(userId: number) {
  return useQuery({
    queryKey: ["lists", "user", userId],
    queryFn: () => api<MovieListSummary[]>(`/api/users/${userId}/lists`),
  });
}

export function useMyLists() {
  return useQuery({
    queryKey: ["lists", "mine"],
    queryFn: () => api<MovieListSummary[]>("/api/lists"),
  });
}

export function useListsContaining(tmdbId: number) {
  return useQuery({
    queryKey: ["lists", "containing", tmdbId],
    queryFn: () => api<number[]>(`/api/lists/containing/${tmdbId}`),
  });
}

export function useMovieList(listId: number) {
  return useQuery({
    queryKey: ["lists", listId],
    queryFn: () => api<MovieList>(`/api/lists/${listId}`),
    enabled: Number.isInteger(listId) && listId > 0,
  });
}

export function useMovieListMovies(listId: number) {
  return useQuery({
    queryKey: ["lists", listId, "movies"],
    queryFn: () => api<MovieListItem[]>(`/api/lists/${listId}/movies`),
    enabled: Number.isInteger(listId) && listId > 0,
  });
}

// Creates a list when id is null, otherwise updates it.
export function useSaveMovieList() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: number | null; input: MovieListInput }) =>
      id === null
        ? api<MovieList>("/api/lists", { method: "POST", body: JSON.stringify(input) })
        : api<MovieList>(`/api/lists/${id}`, { method: "PUT", body: JSON.stringify(input) }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["lists"] }),
  });
}

export function useDeleteMovieList() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api<void>(`/api/lists/${id}`, { method: "DELETE" }),
    onSuccess: (_data, id) => queryClient.removeQueries({ queryKey: ["lists", id] }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["lists"] }),
  });
}

export function useToggleListMovie() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ listId, tmdbId, inList }: { listId: number; tmdbId: number; inList: boolean }) =>
      inList
        ? api<void>(`/api/lists/${listId}/movies/${tmdbId}`, { method: "DELETE" })
        : api<void>(`/api/lists/${listId}/movies`, { method: "POST", body: JSON.stringify({ tmdbId }) }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["lists"] }),
  });
}
