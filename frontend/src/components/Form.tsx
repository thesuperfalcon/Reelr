import { useEffect, useRef, useState, type InputHTMLAttributes } from "react";
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

// A password field with a button that shows what was typed, so a typo can be seen before it is sent.
export function PasswordField({
  id,
  label,
  hint,
  ...input
}: { id: string; label: string; hint?: string } & Omit<InputHTMLAttributes<HTMLInputElement>, "type">) {
  const [visible, setVisible] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Hidden again when the form is sent, so the browser never sees the password in a plain text field (it may
  // keep plain text fields in its form history). This listener on the form runs before React's onSubmit.
  useEffect(() => {
    const field = inputRef.current;
    const form = field?.form;
    if (!field || !form) {
      return;
    }

    function hide() {
      field!.type = "password";
      setVisible(false);
    }

    form.addEventListener("submit", hide);
    return () => form.removeEventListener("submit", hide);
  }, []);

  return (
    <div>
      <label htmlFor={id} className="label">
        {label}
      </label>
      <div className="relative mt-1.5">
        <input
          ref={inputRef}
          id={id}
          name={id}
          required
          type={visible ? "text" : "password"}
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          aria-describedby={hint ? `${id}-hint` : undefined}
          className="input pr-16"
          {...input}
        />
        <button
          type="button"
          onClick={() => setVisible((shown) => !shown)}
          aria-controls={id}
          aria-label={`${visible ? "Hide" : "Show"} ${label.toLowerCase()}`}
          className="absolute inset-y-0 right-0 rounded-r-sm px-3 text-sm font-medium text-haze hover:text-screen focus-visible:outline-2 focus-visible:outline-projector"
        >
          {visible ? "Hide" : "Show"}
        </button>
      </div>
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
