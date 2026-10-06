import { useEffect, useId, useRef, useState } from "react";
import { useSaveMovieList } from "../lib/queries";
import type { MovieList } from "../lib/types";

// Match CreateMovieListDto on the server.
const NAME_MAX = 100;
const DESCRIPTION_MAX = 1000;

interface ListFormDialogProps {
  /** The list to edit, or null to create a new one. */
  current: MovieList | null;
  onSaved?: (list: MovieList) => void;
  onClose: () => void;
}

export function ListFormDialog({ current, onSaved, onClose }: ListFormDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const nameId = useId();
  const descriptionId = useId();
  const [name, setName] = useState(current?.name ?? "");
  const [description, setDescription] = useState(current?.description ?? "");
  const [isPublic, setIsPublic] = useState(current?.isPublic ?? true);
  const save = useSaveMovieList();

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  function submit() {
    save.mutate(
      { id: current?.id ?? null, input: { name: name.trim(), description: description.trim(), isPublic } },
      {
        onSuccess: (list) => {
          onSaved?.(list);
          onClose();
        },
      },
    );
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      // A click on the backdrop lands on the dialog element itself.
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
    >
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        className="p-6"
      >
        <h2 id={titleId} className="marquee text-3xl">
          {current ? "Edit list" : "New list"}
        </h2>

        <div className="mt-6">
          <div className="flex items-baseline justify-between">
            <label htmlFor={nameId} className="text-sm text-haze">
              Name
            </label>
            <span className="text-xs text-haze tabular-nums">
              {name.length} / {NAME_MAX}
            </span>
          </div>
          <input
            id={nameId}
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={NAME_MAX}
            required
            autoFocus
            className="mt-2 block w-full rounded-sm bg-salon px-3 py-2 text-screen ring-1 ring-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-projector"
            placeholder="Best of the year"
          />
        </div>

        <div className="mt-5">
          <div className="flex items-baseline justify-between">
            <label htmlFor={descriptionId} className="text-sm text-haze">
              Description (optional)
            </label>
            <span className="text-xs text-haze tabular-nums">
              {description.length} / {DESCRIPTION_MAX.toLocaleString("en")}
            </span>
          </div>
          <textarea
            id={descriptionId}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            maxLength={DESCRIPTION_MAX}
            rows={4}
            className="mt-2 block w-full resize-y rounded-sm bg-salon px-3 py-2 text-screen ring-1 ring-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-projector"
          />
        </div>

        <label className="mt-5 flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            checked={isPublic}
            onChange={(event) => setIsPublic(event.target.checked)}
            className="mt-0.5 size-4 accent-projector"
          />
          <span>
            <span className="font-medium">Public</span>
            <span className="block text-haze">
              {isPublic ? "Anyone can see this list on your profile." : "Only you can see this list."}
            </span>
          </span>
        </label>

        {save.error && (
          <p role="alert" className="mt-4 text-sm text-alarm">
            {save.error.message}
          </p>
        )}

        <div className="mt-6 flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-sm px-4 py-2 text-sm font-medium text-haze ring-1 ring-white/15 transition hover:text-screen"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={save.isPending || name.trim() === ""}
            className="rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-50"
          >
            {save.isPending ? "Saving…" : current ? "Save" : "Create list"}
          </button>
        </div>
      </form>
    </dialog>
  );
}
