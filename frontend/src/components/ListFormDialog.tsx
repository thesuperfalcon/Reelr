import { useId, useState } from "react";
import { useSaveMovieList } from "../lib/queries";
import type { MovieList } from "../lib/types";
import { Dialog } from "./Dialog";

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
  const titleId = useId();
  const nameId = useId();
  const descriptionId = useId();
  const [name, setName] = useState(current?.name ?? "");
  const [description, setDescription] = useState(current?.description ?? "");
  const [isPublic, setIsPublic] = useState(current?.isPublic ?? true);
  const save = useSaveMovieList();

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
    <Dialog labelledBy={titleId} onClose={onClose} className="w-[min(32rem,calc(100vw-2rem))]">
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        className="p-6"
      >
        <h2 id={titleId} className="heading-section">
          {current ? "Edit list" : "New list"}
        </h2>

        <div className="mt-6">
          <div className="flex items-baseline justify-between">
            <label htmlFor={nameId} className="label">
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
            className="input mt-2 block"
            placeholder="Best of the year"
          />
        </div>

        <div className="mt-5">
          <div className="flex items-baseline justify-between">
            <label htmlFor={descriptionId} className="label">
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
            className="input mt-2 block resize-y"
          />
        </div>

        <label className="mt-5 flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            checked={isPublic}
            onChange={(event) => setIsPublic(event.target.checked)}
            className="check mt-0.5"
          />
          <span>
            <span className="font-medium">Public</span>
            <span className="block text-haze">
              {isPublic ? "Anyone can see this list on your profile." : "Only you can see this list."}
            </span>
          </span>
        </label>

        {save.error && (
          <p role="alert" className="mt-4 rounded-sm border-l-2 border-alarm bg-salon px-3 py-2 text-sm">
            {save.error.message}
          </p>
        )}

        <div className="dialog-actions">
          <button
            type="button"
            onClick={onClose}
            className="btn btn-secondary"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={save.isPending || name.trim() === ""}
            className="btn btn-primary"
          >
            {save.isPending ? "Saving…" : current ? "Save" : "Create list"}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
