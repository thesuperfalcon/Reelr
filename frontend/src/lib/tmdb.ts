type ImageSize = "w185" | "w342" | "w500" | "w780" | "w1280" | "original";

export function tmdbImage(path: string | null | undefined, size: ImageSize): string | null {
  return path ? `https://image.tmdb.org/t/p/${size}${path}` : null;
}

export function releaseYear(date: string | null | undefined): string | null {
  return date && date.length >= 4 ? date.slice(0, 4) : null;
}

export function formatRuntime(minutes: number | null | undefined): string | null {
  if (!minutes) {
    return null;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return hours > 0 ? `${hours} h ${rest} min` : `${rest} min`;
}
