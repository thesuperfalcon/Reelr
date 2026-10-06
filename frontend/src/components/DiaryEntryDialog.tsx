import { useEffect, useId, useRef, useState } from "react";
import { localDay } from "../lib/dates";
import { useDeleteDiaryEntry, useUpdateDiaryEntry } from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { DiaryEntry } from "../lib/types";
import { StatusToggle } from "./RatingDialog";
import { StarInput } from "./Stars";
import { WatchedOnField } from "./WatchedOnField";

interface DiaryEntryDialogProps {
  entry: DiaryEntry;
  onClose: () => void;
}

// Edits or deletes one viewing. The film's current rating and watched status are not touched, except that
// deleting the last entry marks the film unwatched. Deleting the entry that logged the review deletes the review.
export function DiaryEntryDialog({ entry, onClose }: DiaryEntryDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const ratingLabelId = useId();
  const statusLabelId = useId();
  const dateLabelId = useId();
  const [watchedOn, setWatchedOn] = useState(() => localDay(new Date(entry.watchedAt)));
  const [rating, setRating] = useState<number | null>(entry.rating);
  const [liked, setLiked] = useState<boolean | null>(entry.liked);
  const [rewatched, setRewatched] = useState(entry.rewatched);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const update = useUpdateDiaryEntry();
  const remove = useDeleteDiaryEntry();
  const busy = update.isPending || remove.isPending;
  const error = update.error ?? remove.error;
  const poster = tmdbImage(entry.posterUrl, "w185");

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

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
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      // A click on the backdrop lands on the dialog element itself.
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[min(28rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
    >
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
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
            <p className="text-sm text-haze">Edit diary entry</p>
            <h2 id={titleId} className="marquee mt-1 text-3xl">
              {entry.title}
            </h2>
          </div>
        </div>

        <div role="group" aria-labelledby={dateLabelId} className="mt-6 border-t border-white/5 pt-6 text-center">
          <p id={dateLabelId} className="text-sm text-haze">
            Watched on
          </p>
          <div className="mt-3">
            <WatchedOnField value={watchedOn} onChange={setWatchedOn} />
          </div>
        </div>

        <div className="mt-4 border-t border-white/5 pt-6 text-center">
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
                  className="text-haze underline underline-offset-4 hover:text-screen"
                >
                  No rating
                </button>
              </>
            )}
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

        <p className="mt-6 text-xs text-haze">
          Changes here only affect this entry. Your current rating and review of the film stay as they are.
        </p>

        {error && (
          <p role="alert" className="mt-4 text-sm text-alarm">
            {error.message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center gap-3">
          {confirmDelete ? (
            <span className="flex items-center gap-3 text-sm">
              <span className="text-haze">
                {entry.hasReview ? "Delete this entry and its review?" : "Delete this entry?"}
              </span>
              <button
                type="button"
                onClick={() => remove.mutate(entry.id, { onSuccess: onClose })}
                disabled={busy}
                className="font-medium text-alarm underline underline-offset-4 disabled:opacity-50"
              >
                {remove.isPending ? "Deleting…" : "Delete"}
              </button>
              <button
                type="button"
                onClick={() => setConfirmDelete(false)}
                disabled={busy}
                className="text-haze underline underline-offset-4 hover:text-screen"
              >
                Keep
              </button>
            </span>
          ) : (
            <button
              type="button"
              onClick={() => setConfirmDelete(true)}
              disabled={busy}
              className="text-sm text-haze underline underline-offset-4 transition hover:text-alarm disabled:opacity-50"
            >
              Delete entry
            </button>
          )}
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
              {update.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      </form>
    </dialog>
  );
}
