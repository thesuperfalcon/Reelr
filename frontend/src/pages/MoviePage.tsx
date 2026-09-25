import { useParams } from "react-router";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { ApiError } from "../lib/api";
import { useMovieDetails, useSimilarMovies } from "../lib/queries";
import { formatRuntime, releaseYear, tmdbImage } from "../lib/tmdb";
import type { CastMember, MovieDetails } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";

function Backdrop({ path }: { path: string | null }) {
  const src = tmdbImage(path, "w1280");
  if (!src) {
    return <div className="h-24" />;
  }

  return (
    <div className="relative h-[40vh] min-h-56 overflow-hidden sm:h-[55vh]" aria-hidden="true">
      <img src={src} alt="" className="h-full w-full object-cover object-top opacity-70" />
      <div className="absolute inset-0 bg-gradient-to-t from-salon via-salon/40 to-salon/10" />
    </div>
  );
}

function Facts({ movie }: { movie: MovieDetails }) {
  const directors = movie.crew.filter((c) => c.job === "Director").map((c) => c.name);
  const writers = [...new Set(movie.crew.filter((c) => c.job === "Screenplay" || c.job === "Writer").map((c) => c.name))];

  const facts = [
    { term: "Directed by", value: directors.join(", ") },
    { term: "Written by", value: writers.join(", ") },
    { term: "Genres", value: movie.genres.map((g) => g.name).join(", ") },
    {
      term: "TMDB score",
      value: movie.voteCount > 0 ? `${movie.voteAverage.toFixed(1)} / 10 from ${movie.voteCount.toLocaleString("en")} votes` : "",
    },
  ].filter((fact) => fact.value);

  return (
    <dl className="mt-8 grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
      {facts.map((fact) => (
        <div key={fact.term}>
          <dt className="text-haze">{fact.term}</dt>
          <dd className="mt-0.5 text-screen">{fact.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function CastList({ cast }: { cast: CastMember[] }) {
  if (cast.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="cast-title" className="mt-16">
      <h2 id="cast-title" className="marquee text-3xl">
        Cast
      </h2>
      <ul className="mt-5 grid grid-cols-2 gap-x-4 gap-y-5 sm:grid-cols-3 lg:grid-cols-5">
        {cast.map((person) => {
          const photo = tmdbImage(person.profilePath, "w185");
          return (
            <li key={`${person.id}-${person.order}`} className="flex items-center gap-3">
              {photo ? (
                <img src={photo} alt="" loading="lazy" className="size-12 shrink-0 rounded-full object-cover" />
              ) : (
                <div className="size-12 shrink-0 rounded-full bg-row" aria-hidden="true" />
              )}
              <div className="min-w-0 text-sm">
                <p className="truncate font-medium">{person.name}</p>
                {person.character && <p className="truncate text-haze">{person.character}</p>}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function SimilarMovies({ tmdbId }: { tmdbId: number }) {
  const similar = useSimilarMovies(tmdbId);
  const movies = similar.data?.results.slice(0, 6) ?? [];

  if (movies.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="similar-title" className="mt-16">
      <h2 id="similar-title" className="marquee text-3xl">
        If you liked this
      </h2>
      <ul className="mt-5 grid grid-cols-3 gap-3 md:grid-cols-6 lg:gap-4">
        {movies.map((movie) => (
          <li key={movie.id}>
            <Poster movie={movie} size="w342" />
          </li>
        ))}
      </ul>
    </section>
  );
}

export function MoviePage() {
  const tmdbId = Number(useParams().tmdbId);
  const details = useMovieDetails(tmdbId);

  if (!Number.isInteger(tmdbId) || tmdbId <= 0 || (details.error instanceof ApiError && details.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (details.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading film" />
      </div>
    );
  }

  if (details.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={details.error} retry={() => details.refetch()} />
      </div>
    );
  }

  const movie = details.data;
  const year = releaseYear(movie.releaseDate);
  const runtime = formatRuntime(movie.runtime);
  const trailer = movie.videos.find((v) => v.site === "YouTube" && v.key);
  const poster = tmdbImage(movie.posterPath, "w500");

  return (
    <article>
      <Backdrop path={movie.backdropPath} />

      <div className="relative mx-auto -mt-32 max-w-6xl px-4 pb-10 sm:-mt-48 sm:px-6">
        <div className="grid gap-8 md:grid-cols-[16rem_1fr] md:gap-12">
          <div className="w-36 sm:w-48 md:w-full">
            {poster ? (
              <img src={poster} alt={`Poster for ${movie.title}`} className="aspect-[2/3] w-full rounded-sm object-cover ring-1 ring-white/10" />
            ) : (
              <div className="aspect-[2/3] w-full rounded-sm bg-row" aria-hidden="true" />
            )}
          </div>

          <div className="max-w-3xl md:pt-24">
            <h1 className="marquee text-6xl sm:text-8xl">{movie.title}</h1>

            <p className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-haze">
              {year && <span>{year}</span>}
              {runtime && <span>{runtime}</span>}
              {movie.originalTitle && movie.originalTitle !== movie.title && <span lang={movie.originalLanguage ?? undefined}>{movie.originalTitle}</span>}
            </p>

            {movie.tagline && <p className="mt-6 text-lg text-projector">{movie.tagline}</p>}
            {movie.overview && <p className="mt-4 max-w-prose text-screen/90">{movie.overview}</p>}

            {trailer && (
              <a
                href={`https://www.youtube.com/watch?v=${trailer.key}`}
                target="_blank"
                rel="noreferrer"
                className="mt-6 inline-block rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110"
              >
                Watch trailer on YouTube
              </a>
            )}

            <Facts movie={movie} />
          </div>
        </div>

        <CastList cast={movie.cast} />
        <SimilarMovies tmdbId={tmdbId} />
      </div>
    </article>
  );
}
