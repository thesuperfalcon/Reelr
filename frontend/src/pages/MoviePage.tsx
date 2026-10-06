import { useState } from "react";
import { Link, useParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { AddToListDialog } from "../components/AddToListDialog";
import { PersonLink } from "../components/PersonLink";
import { Poster } from "../components/Poster";
import { RatingDialog } from "../components/RatingDialog";
import { ReviewText } from "../components/ReviewText";
import { STAR_PATH, Stars } from "../components/Stars";
import { ErrorMessage, Loading } from "../components/Status";
import { UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import {
  useMovieDetails,
  useMovieReviews,
  useMyRating,
  useMyStatus,
  useSimilarMovies,
  useToggleWatchlist,
  useWatchlist,
} from "../lib/queries";
import { formatRuntime, releaseYear, tmdbImage } from "../lib/tmdb";
import type { CastMember, CrewMember, MovieDetails, Review } from "../lib/types";
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

function WatchlistButton({ tmdbId, title }: { tmdbId: number; title: string | null }) {
  const watchlist = useWatchlist();
  const toggle = useToggleWatchlist(tmdbId);
  const onWatchlist = watchlist.data?.some((entry) => entry.tmdbId === tmdbId) ?? false;
  const label = `${onWatchlist ? "Remove" : "Add"} ${title ?? "film"} ${onWatchlist ? "from" : "to"} watchlist`;

  return (
    <button
      type="button"
      onClick={() => toggle.mutate(onWatchlist)}
      disabled={watchlist.isPending || toggle.isPending}
      aria-label={label}
      aria-pressed={onWatchlist}
      title={label}
      className="inline-flex size-9 items-center justify-center rounded-sm text-projector ring-1 ring-white/15 transition hover:bg-row disabled:opacity-50"
    >
      <svg viewBox="0 0 24 24" fill={onWatchlist ? "currentColor" : "none"} stroke="currentColor" strokeWidth="2" strokeLinejoin="round" className="size-5" aria-hidden="true">
        <path d="M6 3h12v18l-6-4-6 4z" />
      </svg>
    </button>
  );
}

function AddToListButton({ movie }: { movie: MovieDetails }) {
  const [open, setOpen] = useState(false);
  const label = `Add ${movie.title ?? "film"} to a list`;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={label}
        title={label}
        aria-haspopup="dialog"
        className="inline-flex size-9 items-center justify-center rounded-sm text-screen ring-1 ring-white/15 transition hover:bg-row"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-5" aria-hidden="true">
          <path d="M4 6h11M4 12h11M4 18h7M18 15v6M15 18h6" />
        </svg>
      </button>
      {open && <AddToListDialog movie={movie} onClose={() => setOpen(false)} />}
    </>
  );
}

function RateButton({ movie, userId }: { movie: MovieDetails; userId: number }) {
  const rating = useMyRating(movie.id);
  const status = useMyStatus(movie.id);
  const reviews = useMovieReviews(movie.id);
  const review = reviews.data?.find((r) => r.userId === userId) ?? null;
  const [open, setOpen] = useState(false);
  const score = rating.data?.score ?? null;
  const liked = status.data?.liked === true;
  const rewatched = status.data?.rewatched === true;
  const statusText = [liked && "Liked", rewatched && "Rewatch"].filter(Boolean).join(", ");
  const label =
    (score === null ? `Rate ${movie.title ?? "film"}` : `Your rating: ${score} out of 5 stars. Change rating`) +
    (statusText ? `. ${statusText}` : "");

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        disabled={rating.isPending || status.isPending || reviews.isPending}
        aria-label={label}
        title={label}
        aria-haspopup="dialog"
        className="inline-flex h-9 items-center gap-2 rounded-sm px-3 text-sm font-medium text-screen ring-1 ring-white/15 transition hover:bg-row disabled:opacity-50"
      >
        {score === null ? (
          <>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" className="size-4 text-projector" aria-hidden="true">
              <path d={STAR_PATH} />
            </svg>
            Rate
          </>
        ) : (
          <>
            <span className="text-haze">Rated</span>
            <Stars score={score} className="h-4" />
          </>
        )}
        {rewatched && <span className="text-haze">Rewatch</span>}
        {liked && (
          <span className="text-alarm" aria-hidden="true">
            ♥
          </span>
        )}
      </button>
      {open && (
        <RatingDialog
          movie={movie}
          current={score}
          currentStatus={status.data ?? null}
          currentReview={review}
          onClose={() => setOpen(false)}
        />
      )}
    </>
  );
}

// Names as links to each person's page, one per person even when they hold several jobs.
function PeopleLinks({ people }: { people: CrewMember[] }) {
  const unique = people.filter((p, i) => people.findIndex((q) => q.id === p.id) === i);
  return unique.map((person, i) => (
    <span key={person.id}>
      {i > 0 && ", "}
      <Link to={`/person/${person.id}`} className="hover:text-projector hover:underline hover:underline-offset-4">
        {person.name}
      </Link>
    </span>
  ));
}

function Facts({ movie }: { movie: MovieDetails }) {
  const directors = movie.crew.filter((c) => c.job === "Director");
  const writers = movie.crew.filter((c) => c.job === "Screenplay" || c.job === "Writer");

  const facts = [
    { term: "Directed by", value: directors.length > 0 && <PeopleLinks people={directors} /> },
    { term: "Written by", value: writers.length > 0 && <PeopleLinks people={writers} /> },
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
        {cast.map((person) => (
          <li key={`${person.id}-${person.order}`}>
            <PersonLink id={person.id} name={person.name} profilePath={person.profilePath} detail={person.character} />
          </li>
        ))}
      </ul>
    </section>
  );
}

const reviewDateFormat = new Intl.DateTimeFormat("en", { day: "numeric", month: "short", year: "numeric" });

function ReviewItem({ review, own }: { review: Review; own: boolean }) {
  return (
    <li className="py-6">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
        <UserLink
          user={{ id: review.userId, userName: review.username, profileImageUrl: review.profileImageUrl }}
          label={own ? "Your review" : undefined}
          avatarClassName="size-9 text-lg"
        />
        {review.score !== null && <Stars score={review.score} className="h-3.5" />}
        <time dateTime={review.createdAt} className="text-haze">
          {reviewDateFormat.format(new Date(review.createdAt))}
        </time>
        {review.updatedAt && <span className="text-haze">(edited)</span>}
      </div>
      <ReviewText text={review.text} className="mt-3" />
    </li>
  );
}

// Reviews are written and edited in the rating dialog, so writing one also logs the film.
function Reviews({ tmdbId }: { tmdbId: number }) {
  const { user } = useAuth();
  const reviews = useMovieReviews(tmdbId);
  const own = user ? (reviews.data?.find((r) => r.userId === user.id) ?? null) : null;
  // The user's own review comes first; the rest stay newest first.
  const ordered = own ? [own, ...(reviews.data ?? []).filter((r) => r !== own)] : (reviews.data ?? []);

  return (
    <section aria-labelledby="reviews-title" className="mt-16">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h2 id="reviews-title" className="marquee text-3xl">
          Reviews
          {reviews.data && reviews.data.length > 0 && <span className="ml-2 text-xl text-haze">{reviews.data.length}</span>}
        </h2>
        {user ? (
          reviews.isSuccess && (
            <p className="text-sm text-haze">{own ? "Edit your review with Rate." : "Write a review with Rate."}</p>
          )
        ) : (
          <Link to="/login" className="text-sm font-medium text-projector underline underline-offset-4">
            Log in to write a review
          </Link>
        )}
      </div>

      {reviews.isPending ? (
        <Loading label="Loading reviews" />
      ) : reviews.isError ? (
        <ErrorMessage error={reviews.error} retry={() => reviews.refetch()} />
      ) : ordered.length === 0 ? (
        <p className="py-6 text-haze">No reviews yet.</p>
      ) : (
        <ul className="mt-2 divide-y divide-white/5">
          {ordered.map((review) => (
            <ReviewItem key={review.id} review={review} own={review === own} />
          ))}
        </ul>
      )}
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
  const { user } = useAuth();
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

            {(trailer || user) && (
              <div className="mt-6 flex flex-wrap items-center gap-3">
                {trailer && (
                  <a
                    href={`https://www.youtube.com/watch?v=${trailer.key}`}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-block rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110"
                  >
                    Watch trailer on YouTube
                  </a>
                )}
                {user && <RateButton movie={movie} userId={user.id} />}
                {user && <WatchlistButton tmdbId={tmdbId} title={movie.title} />}
                {user && <AddToListButton movie={movie} />}
              </div>
            )}

            <Facts movie={movie} />
          </div>
        </div>

        <CastList cast={movie.cast} />
        <Reviews tmdbId={tmdbId} />
        <SimilarMovies tmdbId={tmdbId} />
      </div>
    </article>
  );
}
