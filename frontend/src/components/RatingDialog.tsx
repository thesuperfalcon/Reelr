import { useEffect, useId, useRef, useState } from "react";
import { today } from "../lib/dates";
import {
  REVIEW_MAX_LENGTH,
  useDeleteRating,
  useDeleteReview,
  useRemoveWatched,
  useSaveDiaryEntry,
} from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { MovieDetails, Review, Status } from "../lib/types";
import { DeleteReviewButton } from "./DeleteReviewButton";
import { MarkdownEditor } from "./MarkdownEditor";
import { StarInput } from "./Stars";
import { WatchedOnField } from "./WatchedOnField";

interface RatingDialogProps {
  movie: MovieDetails;
  current: number | null;
  currentStatus: Status | null;
  currentReview: Review | null;
  onClose: () => void;
}

export function StatusToggle({
  pressed,
  onToggle,
  children,
}: {
  pressed: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onToggle}
      className={`inline-flex h-9 items-center gap-2 rounded-sm px-3 text-sm font-medium ring-1 transition ${
        pressed ? "bg-row-raised text-screen ring-white/30" : "text-haze ring-white/15 hover:text-screen"
      }`}
    >
      {children}
    </button>
  );
}

export function RatingDialog({ movie, current, currentStatus, currentReview: reviewProp, onClose }: RatingDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const labelId = useId();
  const titleId = useId();
  const statusLabelId = useId();
  const dateLabelId = useId();
  const reviewId = useId();
  const [score, setScore] = useState<number | null>(current);
  const [liked, setLiked] = useState<boolean | null>(currentStatus?.liked ?? null);
  const [rewatched, setRewatched] = useState(currentStatus?.rewatched ?? false);
  // The review list refetches after a delete, so remember the deletion until the prop catches up.
  const [reviewDeleted, setReviewDeleted] = useState(false);
  const currentReview = reviewDeleted ? null : reviewProp;
  const [review, setReview] = useState(reviewProp?.text ?? "");
  const [watchedOn, setWatchedOn] = useState(today);
  const [confirmUnwatch, setConfirmUnwatch] = useState(false);
  const save = useSaveDiaryEntry(movie.id);
  const remove = useDeleteRating(movie.id);
  const removeReview = useDeleteReview();
  const unwatch = useRemoveWatched(movie.id);
  const busy = save.isPending || remove.isPending || removeReview.isPending || unwatch.isPending;
  const error = save.error ?? remove.error ?? removeReview.error ?? unwatch.error;
  const poster = tmdbImage(movie.posterPath, "w185");
  const year = releaseYear(movie.releaseDate);

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  async function submit() {
    const trimmed = review.trim();
    const scoreChanged = score !== null && score !== current;
    const statusChanged =
      liked !== (currentStatus?.liked ?? null) || rewatched !== (currentStatus?.rewatched ?? false);
    const reviewChanged = trimmed !== "" && trimmed !== currentReview?.text;
    // Clearing the text deletes the review. That alone is not a watch, so it logs nothing.
    const reviewCleared = trimmed === "" && currentReview !== null;
    // Picking an earlier day is a log of that viewing on its own.
    const backDated = watchedOn !== today();

    try {
      if (reviewCleared) {
        await removeReview.mutateAsync(currentReview.id);
      }

      if (scoreChanged || statusChanged || reviewChanged || backDated) {
        await save.mutateAsync({
          score: scoreChanged ? score : null,
          liked,
          rewatched,
          review: reviewChanged ? trimmed : null,
          watchedOn: backDated ? watchedOn : null,
        });
      }

      onClose();
    } catch {
      // The mutation keeps the error, and the dialog shows it.
    }
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      // A click on the backdrop lands on the dialog element itself.
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[min(36rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
    >
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        className="p-6"
      >
        <div className="flex items-center gap-4">
          {poster ? (
            <img src={poster} alt="" className="aspect-[2/3] w-14 shrink-0 rounded-sm object-cover" />
          ) : (
            <div className="aspect-[2/3] w-14 shrink-0 rounded-sm bg-row-raised" aria-hidden="true" />
          )}
          <div className="min-w-0">
            <p className="text-sm text-haze">Log</p>
            <h2 id={titleId} className="marquee mt-1 text-3xl">
              {movie.title}
              {year && <span className="ml-2 text-xl text-haze">{year}</span>}
            </h2>
          </div>
        </div>

        <div className="mt-6 border-t border-white/5 pt-6 text-center">
          <p id={labelId} className="text-sm text-haze">
            Your rating
          </p>
          <div className="mt-3">
            <StarInput value={score} onChange={setScore} labelledBy={labelId} />
          </div>
          <p className="mt-2 h-5 text-sm text-projector" aria-live="polite">
            {score !== null && `${score} / 5`}
          </p>
        </div>

        <div className="mt-4 border-t border-white/5 pt-6 text-center">
          <p id={statusLabelId} className="text-sm text-haze">
            Status
          </p>
          <div role="group" aria-labelledby={statusLabelId} className="mt-3 flex justify-center gap-3">
            <StatusToggle pressed={liked === true} onToggle={() => setLiked(liked === true ? null : true)}>
              <span className={liked === true ? "text-alarm" : ""} aria-hidden="true">
                ♥
              </span>
              Liked
            </StatusToggle>
            <StatusToggle pressed={rewatched} onToggle={() => setRewatched(!rewatched)}>
              Rewatch
            </StatusToggle>
          </div>
        </div>

        <div role="group" aria-labelledby={dateLabelId} className="mt-4 border-t border-white/5 pt-6 text-center">
          <p id={dateLabelId} className="text-sm text-haze">
            Watched on
          </p>
          <div className="mt-3">
            <WatchedOnField value={watchedOn} onChange={setWatchedOn} />
          </div>
        </div>

        <div className="mt-4 border-t border-white/5 pt-6">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <label htmlFor={reviewId} className="text-sm text-haze">
              {currentReview ? "Your review" : "Review (optional)"}
            </label>
            {currentReview && (
              <DeleteReviewButton
                reviewId={currentReview.id}
                onDeleted={() => {
                  setReviewDeleted(true);
                  setReview("");
                }}
              />
            )}
          </div>
          <MarkdownEditor
            id={reviewId}
            value={review}
            onChange={setReview}
            maxLength={REVIEW_MAX_LENGTH}
            placeholder="What did you think?"
            hint={
              currentReview
                ? "Saving a changed review logs the film again."
                : "Saving a review logs the film in your diary."
            }
          />
        </div>

        {error && (
          <p role="alert" className="mt-4 text-sm text-alarm">
            {error.message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center gap-3">
          {current !== null && (
            <button
              type="button"
              onClick={() => remove.mutate(undefined, { onSuccess: onClose })}
              disabled={busy}
              className="text-sm text-haze underline underline-offset-4 transition hover:text-alarm disabled:opacity-50"
            >
              Remove rating
            </button>
          )}
          {currentStatus !== null &&
            (confirmUnwatch ? (
              <span className="flex items-center gap-3 text-sm">
                <span className="text-haze">Delete every diary entry for this film, and your review?</span>
                <button
                  type="button"
                  onClick={() => unwatch.mutate(undefined, { onSuccess: onClose })}
                  disabled={busy}
                  className="font-medium text-alarm underline underline-offset-4 disabled:opacity-50"
                >
                  {unwatch.isPending ? "Removing…" : "Remove"}
                </button>
                <button
                  type="button"
                  onClick={() => setConfirmUnwatch(false)}
                  disabled={busy}
                  className="text-haze underline underline-offset-4 hover:text-screen"
                >
                  Keep
                </button>
              </span>
            ) : (
              <button
                type="button"
                onClick={() => setConfirmUnwatch(true)}
                disabled={busy}
                className="text-sm text-haze underline underline-offset-4 transition hover:text-alarm disabled:opacity-50"
              >
                Remove from watched
              </button>
            ))}
          <div className="ml-auto flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-sm px-4 py-2 text-sm font-medium text-haze ring-1 ring-white/15 transition hover:text-screen"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={busy}
              className="rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-50"
            >
              {save.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </form>
    </dialog>
  );
}
