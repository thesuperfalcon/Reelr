import { Link } from "react-router";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { usePopularMovies, useTrendingMovies } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { SearchMovie } from "../lib/types";

function Feature({ movie }: { movie: SearchMovie }) {
  const poster = tmdbImage(movie.poster_path, "w500");
  const year = releaseYear(movie.release_date);

  return (
    <section aria-labelledby="feature-title" className="grid items-end gap-8 md:grid-cols-[minmax(0,20rem)_1fr] md:gap-12">
      <Link to={`/movie/${movie.id}`} className="block w-40 rounded-sm sm:w-56 md:w-full" tabIndex={-1}>
        {poster && (
          <img src={poster} alt="" className="aspect-[2/3] w-full rounded-sm object-cover shadow-[0_30px_80px_-20px_rgba(242,181,68,0.35)]" />
        )}
      </Link>

      <div className="max-w-2xl pb-2">
        <p className="text-sm text-projector">Most watched this week</p>
        <h1 id="feature-title" className="marquee mt-3 text-6xl sm:text-8xl lg:text-9xl">
          <Link to={`/movie/${movie.id}`} className="rounded-sm hover:text-projector">
            {movie.title}
          </Link>
        </h1>
        {year && <p className="mt-4 text-haze">{year}</p>}
        {movie.overview && <p className="mt-4 max-w-prose text-screen/85">{movie.overview}</p>}
      </div>
    </section>
  );
}

function MovieShelf({ title, movies }: { title: string; movies: SearchMovie[] }) {
  return (
    <section aria-label={title} className="mt-16">
      <h2 className="marquee text-3xl text-screen">{title}</h2>
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
  const trending = useTrendingMovies();
  const popular = usePopularMovies();

  if (trending.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading this week's films" />
      </div>
    );
  }

  if (trending.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={trending.error} retry={() => trending.refetch()} />
      </div>
    );
  }

  const [featured, ...rest] = trending.data.results;
  const popularMovies = popular.data?.results.filter((m) => m.id !== featured?.id) ?? [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      {featured && <Feature movie={featured} />}

      <MovieShelf title="Trending this week" movies={rest.slice(0, 12)} />

      {popular.isError ? (
        <ErrorMessage error={popular.error} retry={() => popular.refetch()} />
      ) : (
        popularMovies.length > 0 && <MovieShelf title="Popular right now" movies={popularMovies.slice(0, 12)} />
      )}
    </div>
  );
}
