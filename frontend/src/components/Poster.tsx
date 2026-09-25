import { Link } from "react-router";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { SearchMovie } from "../lib/types";

interface PosterProps {
  movie: SearchMovie;
  size?: "w185" | "w342" | "w500";
  className?: string;
  priority?: boolean;
}

export function Poster({ movie, size = "w342", className = "", priority = false }: PosterProps) {
  const src = tmdbImage(movie.poster_path, size);
  const title = movie.title ?? "Untitled";
  const year = releaseYear(movie.release_date);

  return (
    <Link
      to={`/movie/${movie.id}`}
      className={`group block rounded-sm outline-offset-4 ${className}`}
      aria-label={year ? `${title} (${year})` : title}
    >
      <div className="aspect-[2/3] overflow-hidden rounded-sm bg-row ring-1 ring-white/5 transition group-hover:ring-2 group-hover:ring-projector">
        {src ? (
          <img
            src={src}
            alt=""
            loading={priority ? "eager" : "lazy"}
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="flex h-full items-end p-3">
            <span className="marquee text-2xl text-haze">{title}</span>
          </div>
        )}
      </div>
    </Link>
  );
}
