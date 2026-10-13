import { useId, useState } from "react";
import { useAuth } from "../auth/auth-context";
import { today } from "../lib/dates";
import { clearDraft, readDraft, writeDraft } from "../lib/drafts";
import {
  REVIEW_MAX_LENGTH,
  useDeleteRating,
  useDeleteReview,
  useRemoveWatched,
  useSaveDiaryEntry,
  useSetReviewSpoilers,
} from "../lib/queries";
import { releaseYear } from "../lib/tmdb";
import type { MovieDetails, Review, Status } from "../lib/types";
import { DeleteReviewButton } from "./DeleteReviewButton";
import { Dialog, FilmDialogHeader } from "./Dialog";
import { MarkdownEditor } from "./MarkdownEditor";
import { StarInput } from "./Stars";
import { WatchedOnField } from "./WatchedOnField";
import { useReturnFocus } from "../lib/focus";

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
      className={`btn ${
        pressed ? "bg-row-raised text-screen ring-1 ring-projector ring-inset" : "font-medium text-haze ring-1 ring-rule/20 ring-inset hover:text-screen"
      }`}
    >
      {children}
    </button>
  );
}

export function RatingDialog({ movie, current, currentStatus, currentReview: reviewProp, onClose }: RatingDialogProps) {
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
  // Unsaved review text is kept per user and film, so it survives closing the dialog or an expired session.
  const { user } = useAuth();
  const draftKey = `review:${user?.id}:${movie.id}`;
  const [restoredDraft] = useState(() => {
    const draft = readDraft(draftKey);
    return draft !== null && draft !== (reviewProp?.text ?? "") ? draft : null;
  });
  const [review, setReviewText] = useState(restoredDraft ?? reviewProp?.text ?? "");
  const [showDraftNotice, setShowDraftNotice] = useState(restoredDraft !== null);

  function setReview(text: string) {
    setReviewText(text);
    if (text === (currentReview?.text ?? "")) {
      clearDraft(draftKey);
    } else {
      writeDraft(draftKey, text);
    }
  }

  const [containsSpoilers, setContainsSpoilers] = useState(reviewProp?.containsSpoilers ?? false);
  const [watchedOn, setWatchedOn] = useState(today);
  const [confirmUnwatch, setConfirmUnwatch] = useState(false);
  const confirmUnwatchRef = useReturnFocus(confirmUnwatch);
  const save = useSaveDiaryEntry(movie.id);
  const remove = useDeleteRating(movie.id);
  const removeReview = useDeleteReview();
  const setSpoilers = useSetReviewSpoilers();
  const unwatch = useRemoveWatched(movie.id);
  const busy = save.isPending || remove.isPending || removeReview.isPending || setSpoilers.isPending || unwatch.isPending;
  const error = save.error ?? remove.error ?? removeReview.error ?? setSpoilers.error ?? unwatch.error;
  const year = releaseYear(movie.releaseDate);

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
    // Flipping only the spoiler flag is not a watch either, so it is saved on its own.
    const spoilersOnly =
      !reviewChanged && !reviewCleared && currentReview !== null && containsSpoilers !== currentReview.containsSpoilers;

    try {
      if (reviewCleared) {
        await removeReview.mutateAsync(currentReview.id);
      }

      if (spoilersOnly) {
        await setSpoilers.mutateAsync({ id: currentReview.id, containsSpoilers });
      }

      if (scoreChanged || statusChanged || reviewChanged || backDated) {
        await save.mutateAsync({
          score: scoreChanged ? score : null,
          liked,
          rewatched,
          review: reviewChanged ? trimmed : null,
          containsSpoilers: reviewChanged ? containsSpoilers : null,
          watchedOn: backDated ? watchedOn : null,
        });
      }

      clearDraft(draftKey);
      onClose();
    } catch {
      // The mutation keeps the error, and the dialog shows it.
    }
  }

  return (
    <Dialog labelledBy={titleId} onClose={onClose}>
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          void submit();
        }}
        className="p-6"
      >
        <FilmDialogHeader titleId={titleId} label="Log" title={movie.title} year={year} posterPath={movie.posterPath} />

        <div className="dialog-section text-center">
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

        <div className="dialog-section text-center">
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

        <div role="group" aria-labelledby={dateLabelId} className="dialog-section text-center">
          <p id={dateLabelId} className="text-sm text-haze">
            Watched on
          </p>
          <div className="mt-3">
            <WatchedOnField value={watchedOn} onChange={setWatchedOn} />
          </div>
        </div>

        <div className="dialog-section">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <label htmlFor={reviewId} className="text-sm text-haze">
              {currentReview ? "Your review" : "Review (optional)"}
            </label>
            {currentReview && (
              <DeleteReviewButton
                reviewId={currentReview.id}
                onDeleted={() => {
                  setReviewDeleted(true);
                  setReviewText("");
                  clearDraft(draftKey);
                }}
              />
            )}
          </div>
          {showDraftNotice && (
            <p className="mt-2 flex flex-wrap items-center gap-3 text-sm text-haze" role="status">
              Your unsaved text from last time is back.
              <button
                type="button"
                onClick={() => {
                  setReview(currentReview?.text ?? "");
                  setShowDraftNotice(false);
                }}
                className="link"
              >
                Discard it
              </button>
            </p>
          )}
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
          {review.trim() !== "" && (
            <label className="mt-3 flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={containsSpoilers}
                onChange={(event) => setContainsSpoilers(event.target.checked)}
                className="check"
              />
              Contains spoilers
              <span className="text-haze">· readers must choose to show it</span>
            </label>
          )}
        </div>

        {error && (
          <p role="alert" className="mt-4 rounded-sm border-l-2 border-alarm bg-salon px-3 py-2 text-sm">
            {error.message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center gap-3">
          {current !== null && (
            <button
              type="button"
              onClick={() => remove.mutate(undefined, { onSuccess: onClose })}
              disabled={busy}
              className="link-quiet text-sm hover:text-alarm disabled:opacity-50"
            >
              Remove rating
            </button>
          )}
          {currentStatus !== null &&
            (confirmUnwatch ? (
              <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <span className="text-haze">Delete every diary entry for this film, and your review?</span>
                <button
                  type="button"
                  onClick={() => unwatch.mutate(undefined, { onSuccess: onClose })}
                  disabled={busy}
                  className="link-danger"
                >
                  {unwatch.isPending ? "Removing…" : "Remove"}
                </button>
                <button
                  type="button"
                  autoFocus
                  onClick={() => setConfirmUnwatch(false)}
                  disabled={busy}
                  className="link-quiet"
                >
                  Keep
                </button>
              </span>
            ) : (
              <button
                type="button"
                ref={confirmUnwatchRef}
                onClick={() => setConfirmUnwatch(true)}
                disabled={busy}
                className="link-quiet text-sm hover:text-alarm disabled:opacity-50"
              >
                Remove from watched
              </button>
            ))}
          <div className="ml-auto flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="btn btn-secondary"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={busy}
              className="btn btn-primary min-w-24"
            >
              {save.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}
