import type { ReactNode } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/auth-context";
import { isKnownActivity } from "../lib/activity";
import { useActivityPreview, useFollowingFilms, useSettings, useUpdateSettings, useUserProfile } from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { FollowingFilm } from "../lib/types";
import { ActivityReviewCard } from "./ActivityReviewCard";
import { Stars } from "./Stars";
import { ErrorMessage, Loading } from "./Status";
import { UserAvatar } from "./UserAvatar";

const FILM_COUNT = 6;
const REVIEW_COUNT = 3;

const linkClass = "text-sm font-medium text-projector underline underline-offset-4";

function AllActivityLink({ feed }: { feed?: "community" }) {
  return (
    <Link to={feed ? `/activity?feed=${feed}` : "/activity"} className={linkClass}>
      All activity
    </Link>
  );
}

function SectionHeader({ id, title, children }: { id: string; title: string; children?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
      <h2 id={id} className="marquee text-3xl">
        {title}
      </h2>
      {children && <div className="flex items-baseline gap-5">{children}</div>}
    </div>
  );
}

// One poster per film, with the people who logged it stacked in the corner and the newest one's rating below.
function FilmTile({ film }: { film: FollowingFilm }) {
  const { movie, watchers, watcherCount } = film;
  const newest = watchers[0];
  const poster = tmdbImage(movie.posterUrl, "w342");
  const extra = watcherCount - Math.min(watchers.length, 2);

  return (
    <li className="min-w-0">
      <div className="relative">
        <Link to={`/movie/${movie.tmdbId}`} className="group block rounded-sm outline-offset-4" aria-label={movie.title}>
          <div className="aspect-[2/3] overflow-hidden rounded-sm bg-row ring-1 ring-white/5 transition group-hover:ring-2 group-hover:ring-projector">
            {poster ? (
              <img src={poster} alt="" loading="lazy" className="h-full w-full object-cover" />
            ) : (
              <div className="flex h-full items-end p-3">
                <span className="marquee text-xl text-haze">{movie.title}</span>
              </div>
            )}
          </div>
        </Link>
        <div className="absolute -bottom-2 right-1.5 flex" aria-hidden="true">
          {watchers.slice(0, 2).map((w, i) => (
            <UserAvatar
              key={w.actor.id}
              userName={w.actor.userName}
              imageUrl={w.actor.profileImageUrl}
              className={`size-7 text-sm ring-2 ring-salon ${i > 0 ? "-ml-2" : ""}`}
            />
          ))}
          {extra > 0 && (
            <span className="-ml-2 flex size-7 items-center justify-center rounded-full bg-row-raised text-[0.7rem] font-semibold text-screen ring-2 ring-salon">
              +{extra}
            </span>
          )}
        </div>
      </div>

      <div className="mt-3 text-sm">
        <p className="flex h-4 items-center gap-2">
          {newest.rating !== null && <Stars score={newest.rating} className="h-3.5" />}
          {newest.liked && (
            <span className="text-alarm" aria-label="Liked" title="Liked">
              ♥
            </span>
          )}
        </p>
        <p className="mt-1 truncate text-haze">
          <Link to={`/user/${newest.actor.id}`} className="font-medium text-screen hover:text-projector">
            {newest.actor.userName}
          </Link>
          {watcherCount > 1 && ` and ${watcherCount - 1} more`}
        </p>
      </div>
    </li>
  );
}

function NewFromFriends({ userId }: { userId: number }) {
  const films = useFollowingFilms(FILM_COUNT);
  const profile = useUserProfile(userId);

  let body: ReactNode;
  if (films.isPending) {
    body = <Loading label="Loading what your friends watched" />;
  } else if (films.isError) {
    body = <ErrorMessage error={films.error} retry={() => films.refetch()} />;
  } else if (films.data.length === 0) {
    body = (
      <div className="mt-4 text-haze">
        {profile.data?.followingCount === 0 ? (
          <>
            <p>Follow people to see what they watch.</p>
            <p className="mt-3 flex flex-wrap gap-x-5 gap-y-2">
              <Link to="/search" className={linkClass}>
                Find people to follow
              </Link>
              <Link to="/activity?feed=community" className={linkClass}>
                See what everyone is watching
              </Link>
            </p>
          </>
        ) : (
          <p>Nobody you follow has logged a film in the last year.</p>
        )}
      </div>
    );
  } else {
    body = (
      <ul className="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-6 lg:gap-4">
        {films.data.map((film) => (
          <FilmTile key={film.movie.tmdbId} film={film} />
        ))}
      </ul>
    );
  }

  return (
    <section aria-labelledby="friends-films-title" className="mt-16">
      <SectionHeader id="friends-films-title" title="New from friends">
        <AllActivityLink />
      </SectionHeader>
      {body}
    </section>
  );
}

// The newest reviews from the people you follow. Can be hidden; the choice is a user setting.
function ReviewsFromFriends() {
  const settings = useSettings();
  const update = useUpdateSettings();
  const shown = settings.data?.showFriendReviews;
  const reviews = useActivityPreview("following", "reviewed", REVIEW_COUNT, shown === true);

  if (shown === undefined) {
    return null;
  }

  if (!shown) {
    return (
      <p className="mt-10 text-sm text-haze">
        Reviews from friends are hidden.{" "}
        <button
          type="button"
          onClick={() => update.mutate({ showFriendReviews: true })}
          disabled={update.isPending}
          className={linkClass}
        >
          Show them
        </button>
      </p>
    );
  }

  const items = reviews.data?.items.filter(isKnownActivity) ?? [];
  if (reviews.isPending || (reviews.isSuccess && items.length === 0)) {
    return null;
  }

  return (
    <section aria-labelledby="friends-reviews-title" className="mt-16">
      <SectionHeader id="friends-reviews-title" title="Reviews from friends">
        <button
          type="button"
          onClick={() => update.mutate({ showFriendReviews: false })}
          disabled={update.isPending}
          title="You can turn this back on here or in Settings"
          className="text-sm font-medium text-haze transition-colors hover:text-screen disabled:opacity-50"
        >
          Hide
        </button>
      </SectionHeader>
      {update.isError && (
        <p role="alert" className="mt-2 text-sm text-alarm">
          Could not save that. Try again.
        </p>
      )}
      {reviews.isError ? (
        <ErrorMessage error={reviews.error} retry={() => reviews.refetch()} />
      ) : (
        <ul className="mt-5 grid max-w-3xl gap-3">
          {items.map((item) => (
            <li key={item.id}>
              <ActivityReviewCard item={item} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function RecentReviews() {
  const reviews = useActivityPreview("community", "reviewed", REVIEW_COUNT);
  const items = reviews.data?.items.filter(isKnownActivity) ?? [];

  if (!reviews.isSuccess || items.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="recent-reviews-title" className="mt-16">
      <SectionHeader id="recent-reviews-title" title="Recent reviews on Reelr">
        <AllActivityLink feed="community" />
      </SectionHeader>
      <ul className="mt-5 grid max-w-3xl gap-3">
        {items.map((item) => (
          <li key={item.id}>
            <ActivityReviewCard item={item} />
          </li>
        ))}
      </ul>
    </section>
  );
}

// The start page's slice of activity. It stays the same height however much happens; the full stream is on /activity.
export function HomeActivity() {
  const { user } = useAuth();

  if (!user) {
    return <RecentReviews />;
  }

  return (
    <>
      <NewFromFriends userId={user.id} />
      <ReviewsFromFriends />
    </>
  );
}
