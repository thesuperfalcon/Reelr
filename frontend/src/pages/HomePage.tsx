import { Link } from "react-router";
import { HomeActivity } from "../components/HomeActivity";
import { Poster } from "../components/Poster";
import { ErrorMessage, PosterGridSkeleton } from "../components/Status";
import { Welcome } from "../components/Welcome";
import { usePopularMovies, useTrendingMovies } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { SearchMovie } from "../lib/types";
import { usePageTitle } from "../lib/title";

function Feature({ movie }: { movie: SearchMovie }) {
  const poster = tmdbImage(movie.poster_path, "w500");
  const year = releaseYear(movie.release_date);

  return (
    <section aria-labelledby="feature-title" className="grid items-end gap-8 md:grid-cols-[minmax(0,20rem)_1fr] md:gap-12">
      <Link to={`/movie/${movie.id}`} className="block w-40 rounded-sm sm:w-56 md:w-full" tabIndex={-1} aria-hidden="true">
        {poster && (
          <img src={poster} alt="" className="aspect-[2/3] w-full rounded-sm object-cover shadow-[0_30px_80px_-20px_color-mix(in_srgb,var(--color-projector)_35%,transparent)]" />
        )}
      </Link>

      <div className="max-w-2xl pb-2">
        <p className="eyebrow">Most watched this week</p>
        <h1 id="feature-title" className="marquee mt-3 text-6xl sm:text-8xl lg:text-9xl">
          <Link to={`/movie/${movie.id}`} className="rounded-sm hover:text-projector">
            {movie.title}
          </Link>
        </h1>
        {year && <p className="mt-4 text-haze">{year}</p>}
        {movie.overview && <p className="mt-4 line-clamp-5 max-w-prose text-screen/85">{movie.overview}</p>}
      </div>
    </section>
  );
}

function MovieShelf({ title, movies }: { title: string; movies: SearchMovie[] }) {
  return (
    <section aria-label={title} className="mt-16">
      <h2 className="heading-section">{title}</h2>
      <ul className="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6 lg:gap-4">
        {movies.map((movie) => (
          <li key={movie.id}>
            <Poster movie={movie} size="w342" />
          </li>
        ))}
      </ul>
    </section>
  );
}

export function HomePage() {
  usePageTitle("");
  const trending = useTrendingMovies();
  const popular = usePopularMovies();

  if (trending.isPending) {
    return (
      <div className="page py-10 sm:py-14">
        <Welcome />
        {/* The same shapes as the feature and shelf, so nothing jumps when they arrive. */}
        <div role="status" className="grid items-end gap-8 md:grid-cols-[minmax(0,20rem)_1fr] md:gap-12">
          <span className="sr-only">Loading this week's films…</span>
          <div className="skeleton aspect-[2/3] w-40 sm:w-56 md:w-full" aria-hidden="true" />
          <div className="space-y-4 pb-2" aria-hidden="true">
            <div className="skeleton h-4 w-40" />
            <div className="skeleton h-20 w-4/5 sm:h-28" />
            <div className="skeleton h-4 w-full max-w-prose" />
            <div className="skeleton h-4 w-3/4 max-w-prose" />
          </div>
        </div>
        <PosterGridSkeleton label="Loading more films" className="mt-16" />
      </div>
    );
  }

  if (trending.isError) {
    return (
      <div className="page py-10 sm:py-14">
        <Welcome />
        <ErrorMessage error={trending.error} retry={() => trending.refetch()} />
      </div>
    );
  }

  const [featured, ...rest] = trending.data.results;
  const popularMovies = popular.data?.results.filter((m) => m.id !== featured?.id) ?? [];

  return (
    <div className="page py-10 sm:py-14">
      <Welcome />
      {featured && <Feature movie={featured} />}

      <HomeActivity />

      <MovieShelf title="Trending this week" movies={rest.slice(0, 12)} />

      {popular.isError ? (
        <ErrorMessage error={popular.error} retry={() => popular.refetch()} />
      ) : (
        popularMovies.length > 0 && <MovieShelf title="Popular right now" movies={popularMovies.slice(0, 12)} />
      )}
    </div>
  );
}
