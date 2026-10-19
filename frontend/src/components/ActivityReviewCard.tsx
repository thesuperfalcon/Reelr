import { Link } from "react-router";
import { timeAgo } from "../lib/activity";
import { formatDateTime } from "../lib/dates";
import type { ActivityItem } from "../lib/types";
import { FilmLink, SmallPoster } from "./ActivityItem";
import { ReviewCounts } from "./ReviewReactions";
import { ReviewText } from "./ReviewText";
import { Stars } from "./Stars";
import { UserAvatar } from "./UserAvatar";

// A "reviewed" feed item as a card for the start page. The excerpt is cut to three lines so cards line up.
export function ActivityReviewCard({ item }: { item: ActivityItem }) {
  const { actor, movie, review } = item;
  if (!review) {
    return null;
  }

  const occurred = new Date(item.occurredAt);

  return (
    <article className="surface flex gap-4 p-4 sm:p-5">
      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-haze">
          <Link to={`/user/${actor.id}`} className="group flex items-center gap-2 rounded-sm">
            <UserAvatar userName={actor.userName} imageUrl={actor.profileImageUrl} className="size-7 text-sm" />
            <span className="font-medium text-screen group-hover:text-projector">{actor.userName}</span>
          </Link>
          {movie && <FilmLink movie={movie} />}
          {item.rating !== null && <Stars score={item.rating} className="h-3.5" />}
          {item.liked && (
            <span className="text-alarm" aria-label="Liked" title="Liked">
              ♥
            </span>
          )}
        </p>

        {/* The feed sends no excerpt for a review with spoilers. */}
        {review.containsSpoilers ? (
          <p className="mt-3 text-sm text-haze">This review contains spoilers.</p>
        ) : (
          <div className="mt-3 line-clamp-3">
            <ReviewText text={review.excerpt} className="text-sm" />
          </div>
        )}

        <p className="mt-2 flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <Link to={`/review/${review.id}`} className="link text-sm">
            Read the full review
          </Link>
          <time dateTime={item.occurredAt} title={formatDateTime(occurred)} className="text-xs text-haze">
            {timeAgo(occurred)}
          </time>
          <ReviewCounts review={review} />
        </p>
      </div>

      {movie && <SmallPoster movie={movie} />}
    </article>
  );
}
