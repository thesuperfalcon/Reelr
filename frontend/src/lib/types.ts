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
}

export interface WatchlistEntry {
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  addedAt: string;
}

export interface DiaryEntry {
  tmdbId: number;
  title: string;
  posterUrl: string | null;
  liked: boolean | null;
  rewatched: boolean;
  rating: number | null;
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
