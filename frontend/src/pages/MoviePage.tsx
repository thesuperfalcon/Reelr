import { Link, useParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { DeleteReviewButton } from "../components/DeleteReviewButton";
import { FilmActions } from "../components/FilmActions";
import { LoadMore } from "../components/LoadMore";
import { PersonLink } from "../components/PersonLink";
import { Poster } from "../components/Poster";
import { ReviewReactions } from "../components/ReviewReactions";
import { ReviewText } from "../components/ReviewText";
import { SpoilerGate, SpoilerTag } from "../components/SpoilerGate";
import { Stars } from "../components/Stars";
import { EmptyState, ErrorMessage, Loading } from "../components/Status";
import { UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import { formatDate } from "../lib/dates";
import {
  pageItems,
  useMovieDetails,
  useMovieReviews,
  useMyReview,
  useSimilarMovies,
} from "../lib/queries";
import { formatRuntime, releaseYear, tmdbImage } from "../lib/tmdb";
import type { CastMember, CrewMember, MovieDetails, Review } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";
import { usePageTitle } from "../lib/title";

function Backdrop({ path }: { path: string | null }) {
  const src = tmdbImage(path, "w1280");
  if (!src) {
    return <div className="h-24" />;
  }

  return (
    <div className="relative h-[40vh] min-h-56 overflow-hidden sm:h-[55vh]" aria-hidden="true">
      <img src={src} alt="" className="h-full w-full object-cover object-top opacity-60" />
      <div className="absolute inset-0 bg-gradient-to-t from-salon via-salon/40 to-salon/10" />
    </div>
  );
}

// Names as links to each person's page, one per person even when they hold several jobs.
function PeopleLinks({ people }: { people: CrewMember[] }) {
  const unique = people.filter((p, i) => people.findIndex((q) => q.id === p.id) === i);
  return unique.map((person, i) => (
    <span key={person.id}>
      {i > 0 && ", "}
      <Link to={`/person/${person.id}`} className="rounded-xs underline decoration-rule/25 underline-offset-4 hover:text-projector hover:decoration-projector">
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
    <dl className="mt-8 grid gap-x-8 gap-y-4 border-t border-rule/10 pt-6 text-sm sm:grid-cols-2">
      {facts.map((fact) => (
        <div key={fact.term}>
          <dt className="text-xs font-medium tracking-wide text-haze uppercase">{fact.term}</dt>
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
      <h2 id="cast-title" className="heading-section">
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
        <Link to={`/review/${review.id}`} className="text-haze hover:text-screen hover:underline" title="Open review">
          <time dateTime={review.createdAt}>{formatDate(review.createdAt)}</time>
        </Link>
        {review.updatedAt && <span className="text-haze">(edited)</span>}
        {review.containsSpoilers && <SpoilerTag />}
        {own && <DeleteReviewButton reviewId={review.id} className="ml-auto" />}
      </div>
      <SpoilerGate active={review.containsSpoilers && !own}>
        <ReviewText text={review.text} className="mt-3" />
      </SpoilerGate>
      <ReviewReactions review={review} className="mt-3" />
    </li>
  );
}

// Reviews are written and edited in the log dialog, opened from the film's actions.
function Reviews({ tmdbId }: { tmdbId: number }) {
  const { user } = useAuth();
  const reviews = useMovieReviews(tmdbId);
  const myReview = useMyReview(tmdbId, user !== null);
  const own = user ? (myReview.data ?? null) : null;
  const total = reviews.data?.pages[0].totalCount ?? 0;
  // The user's own review comes first; the rest stay newest first.
  const others = pageItems(reviews.data).filter((r) => r.id !== own?.id);
  const ordered = own ? [own, ...others] : others;

  return (
    <section aria-labelledby="reviews-title" className="mt-16">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h2 id="reviews-title" className="heading-section">
          Reviews
          {total > 0 && <span className="ml-2 text-xl text-haze">{total}</span>}
        </h2>
        {user ? (
          reviews.isSuccess && !myReview.isPending && (
            <p className="text-sm text-haze">{own ? "Edit your review with Edit review or log a date." : "Write a review with Review or log a date."}</p>
          )
        ) : (
          <Link to="/login" state={{ from: `/movie/${tmdbId}` }} className="link text-sm">
            Log in to write a review
          </Link>
        )}
      </div>

      {reviews.isPending ? (
        <Loading label="Loading reviews" />
      ) : reviews.isError ? (
        <ErrorMessage error={reviews.error} retry={() => reviews.refetch()} />
      ) : ordered.length === 0 ? (
        <EmptyState className="mt-6">
          <p>No reviews yet. {user ? "Be the first: log the film and add a few words." : "Log in to write the first one."}</p>
        </EmptyState>
      ) : (
        <>
          <ul className="mt-2 divide-y divide-rule/5">
            {ordered.map((review) => (
              <ReviewItem key={review.id} review={review} own={review === own} />
            ))}
          </ul>
          <LoadMore query={reviews} what="reviews" />
        </>
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
      <h2 id="similar-title" className="heading-section">
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
  usePageTitle(details.data?.title ?? undefined, details.data?.overview);

  if (!Number.isInteger(tmdbId) || tmdbId <= 0 || (details.error instanceof ApiError && details.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (details.isPending) {
    return (
      <div className="page">
        <Loading label="Loading film" />
      </div>
    );
  }

  if (details.isError) {
    return (
      <div className="page">
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

      {/* One grid for the whole page, so the actions card can stay in view beside the text, cast and reviews on
          wide screens. On narrow screens the actions sit right under the title. */}
      <div className="page relative -mt-32 grid gap-x-12 pb-10 sm:-mt-48 md:grid-cols-[16rem_minmax(0,1fr)] md:[grid-template-areas:'poster_header'_'poster_actions'_'poster_body'_'more_more'] lg:grid-cols-[16rem_minmax(0,1fr)_18rem] lg:[grid-template-areas:'poster_header_actions'_'poster_body_actions'_'more_more_actions']">
        <div className="w-36 sm:w-48 md:w-full md:[grid-area:poster]">
          {poster ? (
            <img src={poster} alt={`Poster for ${movie.title}`} className="aspect-[2/3] w-full rounded-sm object-cover ring-1 ring-rule/10" />
          ) : (
            <div className="aspect-[2/3] w-full rounded-sm bg-row" aria-hidden="true" />
          )}
        </div>

        <div className="mt-8 max-w-3xl md:mt-0 md:pt-24 md:[grid-area:header]">
          <h1 className="heading-display">{movie.title}</h1>

          <p className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-haze">
            {year && <span>{year}</span>}
            {runtime && <span>{runtime}</span>}
            {movie.originalTitle && movie.originalTitle !== movie.title && <span lang={movie.originalLanguage ?? undefined}>{movie.originalTitle}</span>}
          </p>
        </div>

        <div className="mt-6 md:[grid-area:actions] lg:mt-24">
          {user ? (
            <FilmActions movie={movie} className="max-w-md lg:sticky lg:rounded-sm lg:bg-row lg:ring-1 lg:ring-rule/5 lg:top-6 lg:max-w-none lg:p-5" />
          ) : (
            <Link to="/login" state={{ from: `/movie/${tmdbId}` }} className="btn btn-primary">
              Log in to rate or log
            </Link>
          )}
        </div>

        <div className="max-w-3xl md:[grid-area:body]">
          {movie.tagline && <p className="mt-6 text-lg text-projector italic">{movie.tagline}</p>}
          {movie.overview && <p className="mt-4 max-w-prose text-screen/90">{movie.overview}</p>}

          {trailer && (
            <a
              href={`https://www.youtube.com/watch?v=${trailer.key}`}
              target="_blank"
              rel="noreferrer"
              className="btn btn-secondary mt-6"
            >
              <svg viewBox="0 0 24 24" fill="currentColor" className="size-3.5" aria-hidden="true">
                <path d="M7 4.5v15l12-7.5z" />
              </svg>
              Trailer
              <span className="sr-only">on YouTube (opens in a new tab)</span>
            </a>
          )}

          <Facts movie={movie} />
        </div>

        <div className="min-w-0 md:[grid-area:more]">
          <CastList cast={movie.cast} />
          <Reviews tmdbId={tmdbId} />
          <SimilarMovies tmdbId={tmdbId} />
        </div>
      </div>
    </article>
  );
}
