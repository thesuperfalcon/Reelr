import { useId, useState } from "react";
import { localDay } from "../lib/dates";
import { useDeleteDiaryEntry, useUpdateDiaryEntry } from "../lib/queries";
import type { DiaryEntry } from "../lib/types";
import { Dialog, FilmDialogHeader } from "./Dialog";
import { StatusToggle } from "./RatingDialog";
import { StarInput } from "./Stars";
import { WatchedOnField } from "./WatchedOnField";
import { useReturnFocus } from "../lib/focus";

interface DiaryEntryDialogProps {
  entry: DiaryEntry;
  onClose: () => void;
}

// Edits or deletes one viewing. The film's current rating and watched status are not touched, except that
// deleting the last entry marks the film unwatched. Deleting the entry that logged the review deletes the review.
export function DiaryEntryDialog({ entry, onClose }: DiaryEntryDialogProps) {
  const titleId = useId();
  const ratingLabelId = useId();
  const statusLabelId = useId();
  const dateLabelId = useId();
  const [watchedOn, setWatchedOn] = useState(() => localDay(new Date(entry.watchedAt)));
  const [rating, setRating] = useState<number | null>(entry.rating);
  const [liked, setLiked] = useState<boolean | null>(entry.liked);
  const [rewatched, setRewatched] = useState(entry.rewatched);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const confirmDeleteRef = useReturnFocus(confirmDelete);
  const update = useUpdateDiaryEntry();
  const remove = useDeleteDiaryEntry();
  const busy = update.isPending || remove.isPending;
  const error = update.error ?? remove.error;

  function submit() {
    const unchanged =
      watchedOn === localDay(new Date(entry.watchedAt)) &&
      rating === entry.rating &&
      liked === entry.liked &&
      rewatched === entry.rewatched;

    if (unchanged) {
      onClose();
      return;
    }

    update.mutate({ id: entry.id, update: { watchedOn, rating, liked, rewatched } }, { onSuccess: onClose });
  }

  return (
    <Dialog labelledBy={titleId} onClose={onClose} className="w-[min(28rem,calc(100vw-2rem))]">
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        className="p-6"
      >
        <FilmDialogHeader titleId={titleId} label="Edit diary entry" title={entry.title} posterPath={entry.posterUrl} />

        <div role="group" aria-labelledby={dateLabelId} className="dialog-section text-center">
          <p id={dateLabelId} className="text-sm text-haze">
            Watched on
          </p>
          <div className="mt-3">
            <WatchedOnField value={watchedOn} onChange={setWatchedOn} />
          </div>
        </div>

        <div className="dialog-section text-center">
          <p id={ratingLabelId} className="text-sm text-haze">
            Rating for this viewing
          </p>
          <div className="mt-3">
            <StarInput value={rating} onChange={setRating} labelledBy={ratingLabelId} />
          </div>
          <p className="mt-2 flex h-5 items-center justify-center gap-3 text-sm" aria-live="polite">
            {rating !== null && (
              <>
                <span className="text-projector">{rating} / 5</span>
                <button
                  type="button"
                  onClick={() => setRating(null)}
                  className="link-quiet"
                >
                  No rating
                </button>
              </>
            )}
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

        <p className="mt-6 text-xs text-haze">
          Changes here only affect this entry. Your current rating and review of the film stay as they are.
        </p>

        {error && (
          <p role="alert" className="mt-4 rounded-sm border-l-2 border-alarm bg-salon px-3 py-2 text-sm">
            {error.message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center gap-3">
          {confirmDelete ? (
            <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <span className="text-haze">
                {entry.hasReview ? "Delete this entry and its review?" : "Delete this entry?"}
              </span>
              <button
                type="button"
                onClick={() => remove.mutate(entry.id, { onSuccess: onClose })}
                disabled={busy}
                className="link-danger"
              >
                {remove.isPending ? "Deleting…" : "Delete"}
              </button>
              <button
                type="button"
                autoFocus
                onClick={() => setConfirmDelete(false)}
                disabled={busy}
                className="link-quiet"
              >
                Keep
              </button>
            </span>
          ) : (
            <button
              type="button"
              ref={confirmDeleteRef}
              onClick={() => setConfirmDelete(true)}
              disabled={busy}
              className="link-quiet text-sm hover:text-alarm disabled:opacity-50"
            >
              Delete entry
            </button>
          )}
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
              {update.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </form>
    </Dialog>
  );
}
