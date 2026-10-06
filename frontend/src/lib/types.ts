// Shapes returned by the Reelr API. TMDB passthrough DTOs keep TMDB's snake_case names.

export interface SearchMovie {
  id: number;
  title: string | null;
  overview: string | null;
  poster_path: string | null;
  release_date: string | null;
  vote_average: number;
}

export interface SearchResult {
  page: number;
  results: SearchMovie[];
}

export interface Person {
  id: number;
  name: string | null;
  profile_path: string | null;
  known_for_department: string | null;
  popularity: number;
}

export interface Company {
  id: number;
  name: string | null;
  logo_path: string | null;
}

export interface SearchAllResult {
  page: number;
  movies: SearchMovie[];
  cast: Person[];
  crew: Person[];
  studios: Company[];
}

export interface Genre {
  id: number;
  name: string | null;
}

export interface CastMember {
  id: number;
  name: string | null;
  character: string | null;
  profilePath: string | null;
  order: number;
}

export interface CrewMember {
  id: number;
  name: string | null;
  job: string | null;
  department: string | null;
  profilePath: string | null;
}

export interface Video {
  key: string | null;
  name: string | null;
  site: string | null;
  type: string | null;
  official: boolean;
}

export interface MovieImage {
  filePath: string | null;
  width: number;
  height: number;
  aspectRatio: number;
}

export interface MovieDetails {
  id: number;
  title: string | null;
  originalTitle: string | null;
  overview: string | null;
  tagline: string | null;
  releaseDate: string | null;
  runtime: number | null;
  posterPath: string | null;
  backdropPath: string | null;
  genres: Genre[];
  originalLanguage: string | null;
  imdbId: string | null;
  voteAverage: number;
  voteCount: number;
  cast: CastMember[];
  crew: CrewMember[];
  videos: Video[];
  images: MovieImage[];
}

export interface PersonCredit {
  tmdbId: number;
  title: string | null;
  posterPath: string | null;
  releaseDate: string | null;
  voteAverage: number;
  voteCount: number;
  popularity: number;
  /** "Acting", a crew department such as "Directing", or "Appearances" for roles as themselves. */
  department: string;
  /** Characters played or crew jobs held on this film. */
  roles: string[];
}

export interface PersonDetails {
  id: number;
  name: string | null;
  biography: string | null;
  birthday: string | null;
  deathday: string | null;
  placeOfBirth: string | null;
  profilePath: string | null;
  knownForDepartment: string | null;
  homepage: string | null;
  imdbId: string | null;
  instagramId: string | null;
  twitterId: string | null;
  facebookId: string | null;
  knownFor: PersonCredit[];
  credits: PersonCredit[];
}

export interface CurrentUser {
  id: number;
  username: string;
}

export interface UserProfile {
  id: number;
  userName: string;
  profileImageUrl: string | null;
  followerCount: number;
  followingCount: number;
  /** True when the logged-in user follows this user. */
  isFollowing: boolean;
  watchlistVisibility: WatchlistVisibility;
  /** Whether the logged-in user (or a visitor) may open this user's watchlist. */
  canSeeWatchlist: boolean;
}

export type WatchlistVisibility = "Public" | "Followers" | "Private";

export interface WatchlistEntry {
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  addedAt: string;
}

export interface DiaryEntry {
  id: number;
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  liked: boolean | null;
  rewatched: boolean;
  rating: number | null;
  hasReview: boolean;
  watchedAt: string;
}

export interface UserSummary {
  id: number;
  userName: string;
  profileImageUrl: string | null;
}

export interface Rating {
  tmdbId: number;
  score: number;
}

export interface Review {
  id: number;
  userId: number;
  username: string;
  profileImageUrl: string | null;
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  text: string;
  /** The author's current rating of the film, or null when they have not rated it. */
  score: number | null;
  /** Watch date of the diary entry that logged the review, or null when it has none. */
  watchedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface Status {
  tmdbId: number;
  liked: boolean | null;
  rewatched: boolean;
  watchedAt: string;
}

export interface MovieListItem {
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  addedAt: string;
}

export interface MovieListSummary {
  id: number;
  name: string;
  isPublic: boolean;
  movieCount: number;
  /** The newest few films, for the poster preview. */
  topMovies: MovieListItem[];
  createdAt: string;
}

export interface MovieList extends MovieListSummary {
  userId: number;
  username: string;
  description: string | null;
  updatedAt: string | null;
}

export interface MovieListInput {
  name: string;
  description: string;
  isPublic: boolean;
}

export type ActivityType = "watched" | "reviewed" | "listCreated" | "listAdded" | "watchlistAdded";

export interface ActivityMovie {
  tmdbId: number;
  title: string;
  posterUrl: string | null;
}

export interface ActivityItem {
  id: string;
  /** Kept as string: the API may add kinds this client does not know yet, which are then skipped. */
  type: ActivityType | (string & {});
  occurredAt: string;
  actor: { id: number; userName: string; profileImageUrl: string | null };
  movie: ActivityMovie | null;
  list: { id: number; name: string; movieCount: number } | null;
  review: { id: number; excerpt: string; isTruncated: boolean } | null;
  rating: number | null;
  liked: boolean | null;
  rewatched: boolean | null;
  watchedAt: string | null;
  /** More than 1 when several items were grouped; the item itself is the newest of them. */
  groupCount: number;
  groupMovies: ActivityMovie[];
}

export interface ActivityPage {
  items: ActivityItem[];
  nextCursor: string | null;
}
