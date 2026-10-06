import { useEffect, useId, useRef, useState } from "react";
import { useListsContaining, useMyLists, useSaveMovieList, useToggleListMovie } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { MovieDetails } from "../lib/types";
import { LockIcon } from "./ListCard";
import { ErrorMessage, Loading } from "./Status";

interface AddToListDialogProps {
  movie: MovieDetails;
  onClose: () => void;
}

// Every list of the user with a check for the ones that hold this film. A click adds or removes it right away.
export function AddToListDialog({ movie, onClose }: AddToListDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const newNameId = useId();
  const lists = useMyLists();
  const containing = useListsContaining(movie.id);
  const toggle = useToggleListMovie();
  const create = useSaveMovieList();
  const [newName, setNewName] = useState("");
  const poster = tmdbImage(movie.posterPath, "w185");
  const year = releaseYear(movie.releaseDate);
  const inLists = new Set(containing.data ?? []);
  const error = toggle.error ?? create.error;

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  async function createAndAdd() {
    const name = newName.trim();
    if (!name) {
      return;
    }

    try {
      const list = await create.mutateAsync({ id: null, input: { name, description: "", isPublic: true } });
      await toggle.mutateAsync({ listId: list.id, tmdbId: movie.id, inList: false });
      setNewName("");
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
      className="m-auto max-h-[calc(100dvh-2rem)] w-[min(28rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
    >
      <div className="p-6">
        <div className="flex items-center gap-4">
          {poster ? (
            <img src={poster} alt="" className="aspect-[2/3] w-14 shrink-0 rounded-sm object-cover" />
          ) : (
            <div className="aspect-[2/3] w-14 shrink-0 rounded-sm bg-row-raised" aria-hidden="true" />
          )}
          <div className="min-w-0">
            <p className="text-sm text-haze">Add to lists</p>
            <h2 id={titleId} className="marquee mt-1 text-3xl">
              {movie.title}
              {year && <span className="ml-2 text-xl text-haze">{year}</span>}
            </h2>
          </div>
        </div>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void createAndAdd();
          }}
          className="mt-6 flex gap-2 border-t border-white/5 pt-6"
        >
          <label htmlFor={newNameId} className="sr-only">
            New list name
          </label>
          <input
            id={newNameId}
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
            maxLength={100}
            placeholder="New list…"
            className="min-w-0 flex-1 rounded-sm bg-salon px-3 py-2 text-sm text-screen ring-1 ring-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-projector"
          />
          <button
            type="submit"
            disabled={newName.trim() === "" || create.isPending}
            className="shrink-0 rounded-sm bg-projector px-3 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-50"
          >
            {create.isPending ? "Creating…" : "Create and add"}
          </button>
        </form>

        {lists.isPending || containing.isPending ? (
          <Loading label="Loading your lists" />
        ) : lists.isError ? (
          <ErrorMessage error={lists.error} retry={() => lists.refetch()} />
        ) : containing.isError ? (
          <ErrorMessage error={containing.error} retry={() => containing.refetch()} />
        ) : lists.data.length === 0 ? (
          <p className="py-6 text-sm text-haze">You have no lists yet. Name one above to start.</p>
        ) : (
          <ul className="mt-4 max-h-80 divide-y divide-white/5 overflow-y-auto">
            {lists.data.map((list) => {
              const inList = inLists.has(list.id);
              const busy = toggle.isPending && toggle.variables?.listId === list.id;
              return (
                <li key={list.id}>
                  <button
                    type="button"
                    aria-pressed={inList}
                    disabled={busy}
                    onClick={() => toggle.mutate({ listId: list.id, tmdbId: movie.id, inList })}
                    className="flex w-full items-center gap-3 px-1 py-3 text-left transition hover:bg-row-raised disabled:opacity-50"
                  >
                    <span
                      className={`flex size-5 shrink-0 items-center justify-center rounded-sm ring-1 ${
                        inList ? "bg-projector text-salon ring-projector" : "ring-white/30"
                      }`}
                      aria-hidden="true"
                    >
                      {inList && (
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" className="size-3.5">
                          <path d="M5 12l5 5 9-10" />
                        </svg>
                      )}
                    </span>
                    <span className="min-w-0 flex-1 truncate font-medium">{list.name}</span>
                    {!list.isPublic && (
                      <span className="text-haze" title="Private list">
                        <LockIcon />
                        <span className="sr-only">(private)</span>
                      </span>
                    )}
                    <span className="shrink-0 text-sm text-haze tabular-nums">{list.movieCount}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}

        {error && (
          <p role="alert" className="mt-4 text-sm text-alarm">
            {error.message}
          </p>
        )}

        <div className="mt-6 flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="rounded-sm px-4 py-2 text-sm font-medium text-haze ring-1 ring-white/15 transition hover:text-screen"
          >
            Done
          </button>
        </div>
      </div>
    </dialog>
  );
}
