import { useQuery } from "@tanstack/react-query";
import { api } from "./api";
import type { MovieDetails, SearchAllResult, SearchResult } from "./types";

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
