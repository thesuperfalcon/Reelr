import { useId, useState } from "react";
import { pageItems, useListsContaining, useMyLists, useSaveMovieList, useToggleListMovie } from "../lib/queries";
import { releaseYear } from "../lib/tmdb";
import type { MovieDetails } from "../lib/types";
import { Dialog, FilmDialogHeader } from "./Dialog";
import { LockIcon } from "./ListCard";
import { LoadMore } from "./LoadMore";
import { EmptyState, ErrorMessage, Loading } from "./Status";

interface AddToListDialogProps {
  movie: MovieDetails;
  onClose: () => void;
}

// Every list of the user with a check for the ones that hold this film. A click adds or removes it right away.
export function AddToListDialog({ movie, onClose }: AddToListDialogProps) {
  const titleId = useId();
  const newNameId = useId();
  const lists = useMyLists();
  const containing = useListsContaining(movie.id);
  const toggle = useToggleListMovie();
  const create = useSaveMovieList();
  const [newName, setNewName] = useState("");
  const year = releaseYear(movie.releaseDate);
  const inLists = new Set(containing.data ?? []);
  const error = toggle.error ?? create.error;

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
    <Dialog labelledBy={titleId} onClose={onClose} className="w-[min(28rem,calc(100vw-2rem))]">
      <div className="p-6">
        <FilmDialogHeader titleId={titleId} label="Add to lists" title={movie.title} year={year} posterPath={movie.posterPath} />

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void createAndAdd();
          }}
          className="dialog-section flex gap-2"
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
            className="input min-w-0 flex-1 py-2 text-sm"
          />
          <button
            type="submit"
            disabled={newName.trim() === "" || create.isPending}
            className="btn btn-primary px-3"
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
        ) : pageItems(lists.data).length === 0 ? (
          <EmptyState className="mt-4">
            <p className="text-sm">You have no lists yet. Name one above to start.</p>
          </EmptyState>
        ) : (
          <div className="mt-4 max-h-80 overflow-y-auto">
            <ul className="divide-y divide-rule/5">
              {pageItems(lists.data).map((list) => {
                const inList = inLists.has(list.id);
                const busy = toggle.isPending && toggle.variables?.listId === list.id;
                return (
                  <li key={list.id}>
                    <button
                      type="button"
                      aria-pressed={inList}
                      disabled={busy}
                      onClick={() => toggle.mutate({ listId: list.id, tmdbId: movie.id, inList })}
                      className="flex min-h-12 w-full items-center gap-3 rounded-sm px-2 py-2.5 text-left transition hover:bg-row-raised disabled:opacity-50"
                    >
                      <span
                        className={`flex size-5 shrink-0 items-center justify-center rounded-sm ring-1 ${
                          inList ? "bg-projector text-salon ring-projector" : "ring-rule/30"
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
            <LoadMore query={lists} what="lists" />
          </div>
        )}

        {error && (
          <p role="alert" className="mt-4 rounded-sm border-l-2 border-alarm bg-salon px-3 py-2 text-sm">
            {error.message}
          </p>
        )}

        <div className="dialog-actions">
          <button
            type="button"
            onClick={onClose}
            className="btn btn-secondary"
          >
            Done
          </button>
        </div>
      </div>
    </Dialog>
  );
}
