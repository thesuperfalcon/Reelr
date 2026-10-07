import { Link } from "react-router";
import { useAuth } from "../auth/auth-context";
import { useLikeReview } from "../lib/queries";
import type { Review } from "../lib/types";

function plural(count: number, word: string) {
  return `${count} ${word}${count === 1 ? "" : "s"}`;
}

// Compact like and comment counts for feed items. Shows nothing when both are zero.
export function ReviewCounts({ review }: { review: { id: number; likeCount: number; commentCount: number } }) {
  if (review.likeCount === 0 && review.commentCount === 0) {
    return null;
  }

  return (
    <span className="text-xs text-haze">
      {review.likeCount > 0 && <span title={plural(review.likeCount, "like")}>♥ {review.likeCount}</span>}
      {review.likeCount > 0 && review.commentCount > 0 && " · "}
      {review.commentCount > 0 && (
        <Link to={`/review/${review.id}#comments`} className="hover:text-screen hover:underline">
          {plural(review.commentCount, "comment")}
        </Link>
      )}
    </span>
  );
}

// Like button and comment count under a review. Signed-out readers and the author see the like count only.
export function ReviewReactions({ review, className = "" }: { review: Review; className?: string }) {
  const { user } = useAuth();
  const like = useLikeReview(review);
  const canLike = user !== null && user.id !== review.userId;

  return (
    <div className={`flex flex-wrap items-center gap-x-5 gap-y-1 text-sm text-haze ${className}`}>
      {canLike ? (
        <button
          type="button"
          aria-pressed={review.likedByMe}
          onClick={() => like.mutate(!review.likedByMe)}
          className="inline-flex items-center gap-1.5 transition hover:text-screen"
          title={review.likedByMe ? "Unlike this review" : "Like this review"}
        >
          <span className={review.likedByMe ? "text-alarm" : ""} aria-hidden="true">
            ♥
          </span>
          {plural(review.likeCount, "like")}
        </button>
      ) : (
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true">♥</span>
          {plural(review.likeCount, "like")}
        </span>
      )}
      <Link to={`/review/${review.id}#comments`} className="hover:text-screen hover:underline">
        {plural(review.commentCount, "comment")}
      </Link>
      {like.isError && (
        <span role="alert" className="text-alarm">
          {like.error.message}
        </span>
      )}
    </div>
  );
}
