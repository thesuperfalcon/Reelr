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
      <label htmlFor={id} className="label">
        {label}
      </label>
      <input
        id={id}
        name={id}
        required
        aria-describedby={hint ? `${id}-hint` : undefined}
        className="input mt-1.5"
        {...input}
      />
      {hint && (
        <p id={`${id}-hint`} className="hint mt-1.5">
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
    <div role="alert" className="rounded-sm border-l-2 border-alarm bg-row px-3 py-2 text-sm text-screen">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );
}
