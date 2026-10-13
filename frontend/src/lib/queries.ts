import { useInfiniteQuery, useMutation, useQuery, useQueryClient, type InfiniteData, type QueryKey } from "@tanstack/react-query";
import { useAuth } from "../auth/auth-context";
import { ApiError, api } from "./api";
import type {
  ActivityPage,
  DiaryEntry,
  FollowingFilm,
  MovieDetails,
  MovieList,
  MovieListInput,
  MovieListItem,
  MovieListSummary,
  MovieReviewPage,
  Page,
  PersonDetails,
  Rating,
  Review,
  ReviewComment,
  ReviewLikeState,
  SearchAllResult,
  SearchResult,
  Status,
  UserProfile,
  UserSettings,
  UserSettingsUpdate,
  UserSummary,
  WatchlistEntry,
  WatchlistState,
} from "./types";

// Every item loaded so far, across pages.
export function pageItems<T>(data: InfiniteData<Page<T>> | undefined): T[] {
  return data?.pages.flatMap((page) => page.items) ?? [];
}

// Lists come one page at a time, each fetched with the cursor of the page before.
// Views show pageItems(query.data) and put LoadMore under them for the rest.
function usePagedQuery<TPage extends Page<unknown>>(queryKey: QueryKey, url: string, enabled = true) {
  return useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => {
      const separator = url.includes("?") ? "&" : "?";
      return api<TPage>(pageParam ? `${url}${separator}cursor=${encodeURIComponent(pageParam)}` : url);
    },
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor,
    enabled,
  });
}

export function useTrendingMovies() {
  return useQuery({
    queryKey: ["movies", "trending"],
    queryFn: () => api<SearchResult>("/api/movies/trending"),
  });
}

export function usePopularMovies() {
  return useQuery({
    queryKey: ["movies", "popular"],
    queryFn: () => api<SearchResult>("/api/movies/popular"),
  });
}

export function useMovieDetails(tmdbId: number) {
  return useQuery({
    queryKey: ["movies", tmdbId, "details"],
    queryFn: () => api<MovieDetails>(`/api/movies/${tmdbId}/details`),
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useSimilarMovies(tmdbId: number) {
  return useQuery({
    queryKey: ["movies", tmdbId, "similar"],
    queryFn: () => api<SearchResult>(`/api/movies/${tmdbId}/recommended`),
    enabled: Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useSearchAll(query: string) {
  const trimmed = query.trim();
  return useQuery({
    queryKey: ["search", trimmed],
    queryFn: () => api<SearchAllResult>(`/api/movies/search/all?query=${encodeURIComponent(trimmed)}`),
    enabled: trimmed.length > 0,
  });
}

export function useUserProfile(userId: number, enabled = true) {
  return useQuery({
    queryKey: ["users", userId],
    queryFn: () => api<UserProfile>(`/api/users/${userId}`),
    enabled,
  });
}

// Watchlist and diary belong to the logged-in user. Logout clears the cache.
export function useWatchlist() {
  return usePagedQuery<Page<WatchlistEntry>>(["me", "watchlist", "pages"], "/api/watchlist");
}

// Whether one film is on the user's watchlist, for its watchlist button.
export function useWatchlistState(tmdbId: number) {
  return useQuery({
    queryKey: ["me", "watchlist", tmdbId],
    queryFn: () => api<WatchlistState>(`/api/watchlist/${tmdbId}`),
  });
}

// Another user's watchlist. The API answers 404 when their visibility setting hides it from the caller.
export function useUserWatchlist(userId: number) {
  return usePagedQuery<Page<WatchlistEntry>>(["users", userId, "watchlist"], `/api/users/${userId}/watchlist`);
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
  return usePagedQuery<Page<DiaryEntry>>(["me", "diary", "pages"], "/api/watched");
}

// The TMDB ids of every film the user has watched, to mark them on list and person pages.
// It sits under the diary key, so everything that refreshes the diary refreshes it too.
export function useWatchedFilms(enabled = true) {
  return useQuery({
    queryKey: ["me", "diary", "films"],
    queryFn: async () => new Set(await api<number[]>("/api/watched/films")),
    enabled,
  });
}

export function usePersonDetails(personId: number) {
  return useQuery({
    queryKey: ["people", personId],
    queryFn: () => api<PersonDetails>(`/api/people/${personId}`),
    enabled: Number.isInteger(personId) && personId > 0,
  });
}

export function useUserDiary(userId: number) {
  return usePagedQuery<Page<DiaryEntry>>(["users", userId, "diary"], `/api/users/${userId}/diary`);
}

export function useFollowList(userId: number, list: "followers" | "following") {
  return usePagedQuery<Page<UserSummary>>(["users", userId, list], `/api/users/${userId}/${list}`);
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
  /** Applies only with a review. Null keeps the review's current flag. */
  containsSpoilers: boolean | null;
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
        // Diary stats are counted from the diary.
        queryClient.invalidateQueries({ queryKey: ["stats"] }),
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
        // Diary stats are counted from the diary.
        queryClient.invalidateQueries({ queryKey: ["stats"] }),
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
      ]),
  });
}

export function useMovieReviews(tmdbId: number) {
  return usePagedQuery<MovieReviewPage>(
    ["reviews", "movie", tmdbId],
    `/api/movies/${tmdbId}/reviews?limit=10`,
    Number.isInteger(tmdbId) && tmdbId > 0,
  );
}

// The signed-in user's review of a film, or null. The API answers 204 when there is none.
export function useMyReview(tmdbId: number, enabled = true) {
  return useQuery({
    queryKey: ["reviews", "movie", tmdbId, "mine"],
    queryFn: async () => (await api<Review | undefined>(`/api/movies/${tmdbId}/reviews/mine`)) ?? null,
    enabled: enabled && Number.isInteger(tmdbId) && tmdbId > 0,
  });
}

export function useUserReviews(userId: number) {
  return usePagedQuery<Page<Review>>(["reviews", "user", userId], `/api/users/${userId}/reviews?limit=10`);
}

export function useReview(reviewId: number) {
  return useQuery({
    queryKey: ["reviews", reviewId],
    queryFn: () => api<Review>(`/api/reviews/${reviewId}`),
    enabled: Number.isInteger(reviewId) && reviewId > 0,
  });
}

// Writes or rewrites a review without logging the film, for a film that is already in the diary.
// The server attaches a new review to the latest diary entry. Reviews written together with a log
// go through useSaveDiaryEntry instead.
export function useWriteReview(tmdbId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, text, containsSpoilers }: { id: number | null; text: string; containsSpoilers: boolean }) =>
      id === null
        ? api<Review>(`/api/movies/${tmdbId}/reviews`, {
            method: "POST",
            body: JSON.stringify({ text, containsSpoilers }),
          })
        : api<Review>(`/api/reviews/${id}`, {
            method: "PUT",
            body: JSON.stringify({ text, containsSpoilers }),
          }),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
        // The diary shows which entries have a review.
        queryClient.invalidateQueries({ queryKey: ["me", "diary"] }),
      ]),
  });
}

export function useDeleteReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api<void>(`/api/reviews/${id}`, { method: "DELETE" }),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
        queryClient.invalidateQueries({ queryKey: ["me", "diary"] }),
        // Diary stats are counted from the diary.
        queryClient.invalidateQueries({ queryKey: ["stats"] }),
      ]),
  });
}

// Changes only the spoiler flag, so it logs nothing in the diary.
export function useSetReviewSpoilers() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, containsSpoilers }: { id: number; containsSpoilers: boolean }) =>
      api<Review>(`/api/reviews/${id}`, {
        method: "PUT",
        body: JSON.stringify({ containsSpoilers }),
      }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["reviews"] }),
  });
}

// Applies a change to one review wherever it is cached: in film and profile lists and on its own page.
function patchCachedReview(queryClient: ReturnType<typeof useQueryClient>, id: number, patch: Partial<Review>) {
  queryClient.setQueriesData<Review | null | InfiniteData<Page<Review>>>({ queryKey: ["reviews"] }, (data) => {
    if (data && "pages" in data) {
      return {
        ...data,
        pages: data.pages.map((page) => ({
          ...page,
          items: page.items.map((review) => (review.id === id ? { ...review, ...patch } : review)),
        })),
      };
    }
    return data?.id === id ? { ...data, ...patch } : data;
  });
}

// Likes show at once and roll back if the request fails.
export function useLikeReview(review: Pick<Review, "id" | "likeCount" | "likedByMe">) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (like: boolean) =>
      api<ReviewLikeState>(`/api/reviews/${review.id}/like`, { method: like ? "PUT" : "DELETE" }),
    onMutate: async (like) => {
      await queryClient.cancelQueries({ queryKey: ["reviews"] });
      const before = { likeCount: review.likeCount, likedByMe: review.likedByMe };
      patchCachedReview(queryClient, review.id, {
        likedByMe: like,
        likeCount: review.likeCount + (like === review.likedByMe ? 0 : like ? 1 : -1),
      });
      return before;
    },
    onSuccess: (state) => patchCachedReview(queryClient, review.id, state),
    onError: (_error, _like, before) => before && patchCachedReview(queryClient, review.id, before),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["reviews"] }),
  });
}

// Matches ReviewComment.MaxLength on the server.
export const COMMENT_MAX_LENGTH = 1000;

// Comments have their own key, so review list updates never touch them.
export function useReviewComments(reviewId: number) {
  return usePagedQuery<Page<ReviewComment>>(
    ["review-comments", reviewId],
    `/api/reviews/${reviewId}/comments`,
    Number.isInteger(reviewId) && reviewId > 0,
  );
}

// Every comment write refreshes the thread and the review's comment count.
function useCommentMutation<TInput, TResult>(reviewId: number, mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["review-comments", reviewId] }),
        queryClient.invalidateQueries({ queryKey: ["reviews"] }),
      ]),
  });
}

export function useAddComment(reviewId: number) {
  return useCommentMutation(reviewId, (text: string) =>
    api<ReviewComment>(`/api/reviews/${reviewId}/comments`, {
      method: "POST",
      body: JSON.stringify({ text }),
    }),
  );
}

export function useUpdateComment(reviewId: number) {
  return useCommentMutation(reviewId, ({ id, text }: { id: number; text: string }) =>
    api<ReviewComment>(`/api/comments/${id}`, {
      method: "PUT",
      body: JSON.stringify({ text }),
    }),
  );
}

export function useDeleteComment(reviewId: number) {
  return useCommentMutation(reviewId, (id: number) => api<void>(`/api/comments/${id}`, { method: "DELETE" }));
}

// Lists change from several places (movie page, profile, list page), so every write refreshes all of them.
export function useUserLists(userId: number) {
  return usePagedQuery<Page<MovieListSummary>>(["lists", "user", userId], `/api/users/${userId}/lists`);
}

export function useMyLists() {
  return usePagedQuery<Page<MovieListSummary>>(["lists", "mine"], "/api/lists");
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
  return usePagedQuery<Page<MovieListItem>>(
    ["lists", listId, "movies"],
    `/api/lists/${listId}/movies?limit=48`,
    Number.isInteger(listId) && listId > 0,
  );
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

export type FeedKind = "following" | "community";

// Feed pages are fetched with the cursor of the previous page. Other people's activity changes without
// anything happening in this tab, so the feed goes stale sooner than other data.
export function useActivityFeed(kind: FeedKind, enabled = true) {
  return useInfiniteQuery({
    queryKey: ["feed", kind],
    queryFn: ({ pageParam }) =>
      api<ActivityPage>(`/api/feed/${kind}?limit=15${pageParam ? `&cursor=${encodeURIComponent(pageParam)}` : ""}`),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor,
    enabled,
    staleTime: 30 * 1000,
  });
}

// A short, fixed slice of a feed for the start page, e.g. the newest few reviews.
// Previews of Following leave out the user's own activity, since they show what friends did.
export function useActivityPreview(kind: FeedKind, types: string, limit: number, enabled = true) {
  const own = kind === "following" ? "&includeOwn=false" : "";
  return useQuery({
    queryKey: ["feed", kind, "preview", types, limit],
    queryFn: () => api<ActivityPage>(`/api/feed/${kind}?limit=${limit}&types=${encodeURIComponent(types)}${own}`),
    enabled,
    staleTime: 30 * 1000,
  });
}

export function useFollowingFilms(limit: number, enabled = true) {
  return useQuery({
    queryKey: ["feed", "following", "films", limit],
    queryFn: () => api<FollowingFilm[]>(`/api/feed/following/films?limit=${limit}`),
    enabled,
    staleTime: 30 * 1000,
  });
}

const settingsKey = ["me", "settings"];

export function useSettings(enabled = true) {
  return useQuery({
    queryKey: settingsKey,
    queryFn: () => api<UserSettings>("/api/settings"),
    enabled,
  });
}

// A new name or picture shows up in profiles, feeds, reviews and lists, so every cached query is refreshed.
function useSettingsSaved() {
  const queryClient = useQueryClient();

  return (settings: UserSettings, refreshAll: boolean) => {
    queryClient.setQueryData(settingsKey, settings);
    if (refreshAll) {
      queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] !== "me" || query.queryKey[1] !== "settings" });
    }
  };
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();
  const saved = useSettingsSaved();
  const { replaceToken } = useAuth();

  return useMutation({
    mutationFn: (update: UserSettingsUpdate) =>
      api<UserSettings & { token: string | null }>("/api/settings", { method: "PATCH", body: JSON.stringify(update) }),
    onSuccess: ({ token, ...settings }, update) => {
      if (token) {
        replaceToken(token);
      }
      saved(
        settings,
        update.userName !== undefined || update.watchlistVisibility !== undefined || update.diaryVisibility !== undefined,
      );
      // The Following feed reads this setting on the server.
      if (update.showOwnActivity !== undefined) {
        void queryClient.invalidateQueries({ queryKey: ["feed", "following"] });
      }
    },
  });
}

// A password change signs out every other device. This one gets a new token and stays logged in.
export function useChangePassword() {
  const { replaceToken } = useAuth();

  return useMutation({
    mutationFn: (input: { currentPassword: string; newPassword: string }) =>
      api<{ token: string }>("/api/settings/password", { method: "POST", body: JSON.stringify(input) }),
    onSuccess: ({ token }) => replaceToken(token),
  });
}

// Revokes every token for the account, this device's included, so the caller logs out afterwards.
export function useLogoutEverywhere() {
  return useMutation({
    mutationFn: () => api<void>("/api/settings/logout-everywhere", { method: "POST" }),
  });
}

// The API asks for the current password again before it deletes the account.
export function useDeleteAccount() {
  return useMutation({
    mutationFn: (input: { userId: number; password: string }) =>
      api<void>(`/api/users/${input.userId}`, { method: "DELETE", body: JSON.stringify({ password: input.password }) }),
  });
}

export function useUploadAvatar() {
  const saved = useSettingsSaved();

  return useMutation({
    mutationFn: (image: Blob) => {
      const form = new FormData();
      form.append("file", image, "avatar");
      return api<UserSettings>("/api/settings/avatar", { method: "PUT", body: form });
    },
    onSuccess: (settings) => saved(settings, true),
  });
}

export function useRemoveAvatar() {
  const saved = useSettingsSaved();

  return useMutation({
    mutationFn: () => api<UserSettings>("/api/settings/avatar", { method: "DELETE" }),
    onSuccess: (settings) => saved(settings, true),
  });
}
