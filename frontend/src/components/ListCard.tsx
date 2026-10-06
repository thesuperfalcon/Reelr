import { Link } from "react-router";
import { tmdbImage } from "../lib/tmdb";
import type { MovieListSummary } from "../lib/types";

export function LockIcon({ className = "size-3.5" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className={className} aria-hidden="true">
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V7a4 4 0 0 1 8 0v4" />
    </svg>
  );
}

const PREVIEW_SLOTS = 3;

// Overlapping posters of the newest films, name and count, linking to the list page.
export function ListCard({ list }: { list: MovieListSummary }) {
  const slots = Array.from({ length: PREVIEW_SLOTS }, (_, i) => list.topMovies[i] ?? null);

  return (
    <Link to={`/list/${list.id}`} className="group block rounded-sm">
      <div className="flex rounded-sm bg-row p-2 ring-1 ring-white/5 transition group-hover:ring-2 group-hover:ring-projector">
        {slots.map((movie, i) => {
          const poster = movie && tmdbImage(movie.posterUrl, "w185");
          return (
            <div
              key={movie?.tmdbId ?? `empty-${i}`}
              className={`aspect-[2/3] w-1/3 overflow-hidden rounded-sm bg-row-raised shadow-lg ring-1 ring-salon ${i > 0 ? "-ml-4" : ""}`}
              style={{ zIndex: PREVIEW_SLOTS - i }}
            >
              {poster && <img src={poster} alt="" loading="lazy" className="h-full w-full object-cover" />}
            </div>
          );
        })}
      </div>
      <p className="mt-2 flex items-center gap-1.5 font-medium group-hover:text-projector">
        <span className="truncate">{list.name}</span>
        {!list.isPublic && (
          <span className="shrink-0 text-haze" title="Private list">
            <LockIcon />
            <span className="sr-only">(private)</span>
          </span>
        )}
      </p>
      <p className="text-sm text-haze">
        {list.movieCount} {list.movieCount === 1 ? "film" : "films"}
      </p>
    </Link>
  );
}
