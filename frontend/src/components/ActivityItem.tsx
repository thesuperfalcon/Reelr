import type { ReactNode } from "react";
import { Link } from "react-router";
import { dayFormat, fullFormat, timeAgo } from "../lib/activity";
import { tmdbImage } from "../lib/tmdb";
import type { ActivityItem as Item, ActivityMovie } from "../lib/types";
import { ReviewText } from "./ReviewText";
import { Stars } from "./Stars";
import { UserAvatar } from "./UserAvatar";

export function FilmLink({ movie }: { movie: ActivityMovie }) {
  return (
    <Link to={`/movie/${movie.tmdbId}`} className="font-medium text-screen hover:text-projector">
      {movie.title}
    </Link>
  );
}

export function SmallPoster({ movie }: { movie: ActivityMovie }) {
  const poster = tmdbImage(movie.posterUrl, "w185");
  return (
    <Link to={`/movie/${movie.tmdbId}`} className="block w-12 shrink-0 rounded-sm sm:w-14" tabIndex={-1} title={movie.title}>
      {poster ? (
        <img src={poster} alt="" loading="lazy" className="aspect-[2/3] w-full rounded-sm object-cover ring-1 ring-white/10" />
      ) : (
        <div className="aspect-[2/3] w-full rounded-sm bg-row" aria-hidden="true" />
      )}
    </Link>
  );
}

// Watched date, when it is not simply the day the entry was logged.
function watchedOnText(item: Item): string | null {
  if (!item.watchedAt) {
    return null;
  }

  const watched = new Date(item.watchedAt);
  const logged = new Date(item.occurredAt);
  return Math.abs(logged.getTime() - watched.getTime()) > 36 * 60 * 60 * 1000 ? `watched ${dayFormat.format(watched)}` : null;
}

function Details({ item }: { item: Item }) {
  const watchedOn = watchedOnText(item);
  if (item.rating === null && !item.liked && !item.rewatched && !watchedOn) {
    return null;
  }

  return (
    <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-haze">
      {item.rating !== null && <Stars score={item.rating} className="h-3.5" />}
      {item.liked && (
        <span className="text-alarm" aria-label="Liked" title="Liked">
          ♥
        </span>
      )}
      {item.rewatched && <span>Rewatch</span>}
      {watchedOn && <span>{watchedOn}</span>}
    </p>
  );
}

// One feed row: who did what, with every name, film, list, review and watchlist clickable.
export function ActivityItem({ item }: { item: Item }) {
  const { actor, movie, list, review } = item;
  const grouped = item.groupCount > 1;
  const userUrl = `/user/${actor.id}`;
  const who = (
    <Link to={userUrl} className="font-medium text-screen hover:text-projector">
      {actor.userName}
    </Link>
  );

  let action: ReactNode;
  switch (item.type) {
    case "watched":
      action = grouped ? (
        <>
          {who} logged {item.groupCount} films
        </>
      ) : (
        <>
          {who} watched {movie && <FilmLink movie={movie} />}
        </>
      );
      break;
    case "reviewed":
      action = (
        <>
          {who} reviewed {movie && <FilmLink movie={movie} />}
        </>
      );
      break;
    case "listCreated":
      action = (
        <>
          {who} created the list{" "}
          {list && (
            <Link to={`/list/${list.id}`} className="font-medium text-screen hover:text-projector">
              {list.name}
            </Link>
          )}
        </>
      );
      break;
    case "listAdded":
      action = (
        <>
          {who} added {grouped ? `${item.groupCount} films` : movie && <FilmLink movie={movie} />} to{" "}
          {list && (
            <Link to={`/list/${list.id}`} className="font-medium text-screen hover:text-projector">
              {list.name}
            </Link>
          )}
        </>
      );
      break;
    default:
      action = (
        <>
          {who} added {movie && <FilmLink movie={movie} />} to their{" "}
          <Link to={`${userUrl}?tab=watchlist`} className="font-medium text-screen hover:text-projector">
            watchlist
          </Link>
        </>
      );
  }

  const occurred = new Date(item.occurredAt);

  return (
    <li className="flex gap-3 py-5 sm:gap-4">
      <Link to={userUrl} className="shrink-0 rounded-full" tabIndex={-1}>
        <UserAvatar userName={actor.userName} imageUrl={actor.profileImageUrl} className="size-10 text-lg" />
      </Link>

      <div className="min-w-0 flex-1">
        <p className="text-haze">{action}</p>
        {!grouped && <Details item={item} />}

        {review && (
          <div className="mt-3">
            <ReviewText text={review.isTruncated ? `${review.excerpt}…` : review.excerpt} className="text-sm" />
            <Link to={`/review/${review.id}`} className="mt-2 inline-block text-sm font-medium text-projector underline underline-offset-4">
              {review.isTruncated ? "Read the full review" : "Open review"}
            </Link>
          </div>
        )}

        {list && item.type === "listCreated" && (
          <p className="mt-1 text-sm text-haze">
            {list.movieCount} {list.movieCount === 1 ? "film" : "films"}
          </p>
        )}

        {grouped && (
          <div className="mt-3">
            <ul className="flex flex-wrap gap-2">
              {item.groupMovies.map((m) => (
                <li key={m.tmdbId}>
                  <SmallPoster movie={m} />
                </li>
              ))}
            </ul>
            <Link
              to={item.type === "listAdded" && list ? `/list/${list.id}` : userUrl}
              className="mt-2 inline-block text-sm font-medium text-projector underline underline-offset-4"
            >
              {item.type === "listAdded" ? "Show the list" : "Show all in their diary"}
            </Link>
          </div>
        )}

        <time dateTime={item.occurredAt} title={fullFormat.format(occurred)} className="mt-2 block text-xs text-haze">
          {timeAgo(occurred)}
        </time>
      </div>

      {!grouped && movie && <SmallPoster movie={movie} />}
    </li>
  );
}
