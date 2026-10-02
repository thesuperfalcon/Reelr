import { ApiError } from "../lib/api";

export function Loading({ label = "Loading" }: { label?: string }) {
  return (
    <p role="status" className="py-10 text-haze">
      {label}…
    </p>
  );
}

export function ErrorMessage({ error, retry }: { error: unknown; retry?: () => void }) {
  const message =
    error instanceof ApiError && error.status >= 500
      ? "The film service is not answering. Try again in a moment."
      : error instanceof Error
        ? error.message
        : "Something went wrong.";

  return (
    <div role="alert" className="py-10">
      <p className="text-alarm">{message}</p>
      {retry && (
        <button
          type="button"
          onClick={retry}
          className="mt-3 text-sm font-medium text-projector underline underline-offset-4"
        >
          Try again
        </button>
      )}
    </div>
  );
}
