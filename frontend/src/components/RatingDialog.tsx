import { useEffect, useId, useRef, useState } from "react";
import { useDeleteRating, useSaveDiaryEntry } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { MovieDetails, Status } from "../lib/types";
import { StarInput } from "./Stars";

interface RatingDialogProps {
  movie: MovieDetails;
  current: number | null;
  currentStatus: Status | null;
  onClose: () => void;
}

function StatusToggle({
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

export function RatingDialog({ movie, current, currentStatus, onClose }: RatingDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const labelId = useId();
  const titleId = useId();
  const statusLabelId = useId();
  const [score, setScore] = useState<number | null>(current);
  const [liked, setLiked] = useState<boolean | null>(currentStatus?.liked ?? null);
  const [rewatched, setRewatched] = useState(currentStatus?.rewatched ?? false);
  const save = useSaveDiaryEntry(movie.id);
  const remove = useDeleteRating(movie.id);
  const busy = save.isPending || remove.isPending;
  const error = save.error ?? remove.error;
  const poster = tmdbImage(movie.posterPath, "w185");
  const year = releaseYear(movie.releaseDate);

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  function submit() {
    const scoreChanged = score !== null && score !== current;
    const statusChanged =
      liked !== (currentStatus?.liked ?? null) || rewatched !== (currentStatus?.rewatched ?? false);

    if (!scoreChanged && !statusChanged) {
      onClose();
      return;
    }

    save.mutate(
      { score: scoreChanged ? score : null, liked, rewatched },
      { onSuccess: onClose },
    );
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      // A click on the backdrop lands on the dialog element itself.
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className="m-auto w-[min(26rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
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
            <p className="text-sm text-haze">Rate</p>
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
