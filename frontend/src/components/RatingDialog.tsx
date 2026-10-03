import { useEffect, useId, useRef, useState } from "react";
import { useDeleteRating, useSaveRating } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { MovieDetails } from "../lib/types";
import { StarInput } from "./Stars";

interface RatingDialogProps {
  movie: MovieDetails;
  current: number | null;
  onClose: () => void;
}

export function RatingDialog({ movie, current, onClose }: RatingDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const labelId = useId();
  const titleId = useId();
  const [score, setScore] = useState<number | null>(current);
  const save = useSaveRating(movie.id);
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
    if (score === null) {
      return;
    }
    if (score === current) {
      onClose();
      return;
    }
    save.mutate({ score, exists: current !== null }, { onSuccess: onClose });
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
              disabled={busy || score === null}
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
