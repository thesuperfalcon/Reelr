import type { InputHTMLAttributes } from "react";
import { ApiError } from "../lib/api";

export function Field({
  id,
  label,
  hint,
  ...input
}: { id: string; label: string; hint?: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <div>
      <label htmlFor={id} className="block text-sm font-medium">
        {label}
      </label>
      <input
        id={id}
        name={id}
        required
        aria-describedby={hint ? `${id}-hint` : undefined}
        className="mt-1.5 w-full rounded-sm bg-row px-3 py-2.5 text-screen focus:bg-row-raised focus:outline-2 focus:outline-projector"
        {...input}
      />
      {hint && (
        <p id={`${id}-hint`} className="mt-1 text-xs text-haze">
          {hint}
        </p>
      )}
    </div>
  );
}

// Field or Identity errors from the API, one per line, or the plain message.
export function FormError({ error }: { error: unknown }) {
  if (!error) {
    return null;
  }

  const messages =
    error instanceof ApiError && error.details.length > 0
      ? error.details
      : [error instanceof Error ? error.message : "Something went wrong."];

  return (
    <div role="alert" className="text-sm text-alarm">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );
}
