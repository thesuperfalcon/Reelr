import { Link, useNavigate, useParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { DeleteReviewButton } from "../components/DeleteReviewButton";
import { ReviewText } from "../components/ReviewText";
import { Stars } from "../components/Stars";
import { ErrorMessage, Loading } from "../components/Status";
import { UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import { useReview } from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import { NotFoundPage } from "./NotFoundPage";

const dateFormat = new Intl.DateTimeFormat("en", { day: "numeric", month: "long", year: "numeric" });

// One review on its own page, the target of review links in lists and the activity feed.
export function ReviewPage() {
  const reviewId = Number(useParams().reviewId);
  const { user } = useAuth();
  const navigate = useNavigate();
  const review = useReview(reviewId);

  if (!Number.isInteger(reviewId) || reviewId <= 0 || (review.error instanceof ApiError && review.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (review.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading review" />
      </div>
    );
  }

  if (review.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={review.error} retry={() => review.refetch()} />
      </div>
    );
  }

  const data = review.data;
  const own = user?.id === data.userId;
  const poster = tmdbImage(data.posterUrl, "w342");
  const filmUrl = `/movie/${data.tmdbId}`;

  return (
    <article className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="grid gap-8 md:grid-cols-[12rem_1fr] md:gap-12">
        <Link to={filmUrl} className="block w-32 rounded-sm sm:w-40 md:w-full" tabIndex={-1}>
          {poster ? (
            <img src={poster} alt="" className="aspect-[2/3] w-full rounded-sm object-cover ring-1 ring-white/10" />
          ) : (
            <div className="aspect-[2/3] w-full rounded-sm bg-row" aria-hidden="true" />
          )}
        </Link>

        <div className="min-w-0">
          <UserLink
            user={{ id: data.userId, userName: data.username, profileImageUrl: data.profileImageUrl }}
            label={own ? "Your review" : `Review by ${data.username}`}
            avatarClassName="size-9 text-lg"
            className="text-sm"
          />

          <h1 className="marquee mt-4 text-5xl sm:text-7xl">
            <Link to={filmUrl} className="hover:text-projector">
              {data.title}
            </Link>
          </h1>

          <div className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-haze">
            {data.score !== null && <Stars score={data.score} className="h-4" />}
            {data.watchedAt && (
              <span>
                Watched <time dateTime={data.watchedAt}>{dateFormat.format(new Date(data.watchedAt))}</time>
              </span>
            )}
            <span>
              Written <time dateTime={data.createdAt}>{dateFormat.format(new Date(data.createdAt))}</time>
              {data.updatedAt && " (edited)"}
            </span>
          </div>

          <ReviewText text={data.text} className="mt-8 text-lg" />

          {own && (
            <div className="mt-8 flex flex-wrap items-center gap-4 border-t border-white/5 pt-6 text-sm">
              <Link to={filmUrl} className="font-medium text-projector underline underline-offset-4">
                Edit with Rate on the film page
              </Link>
              <DeleteReviewButton reviewId={data.id} onDeleted={() => navigate(filmUrl, { replace: true })} />
            </div>
          )}
        </div>
      </div>
    </article>
  );
}
