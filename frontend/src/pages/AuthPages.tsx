import { useState, type FormEvent, type ReactNode } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router";
import { useAuth } from "../auth/auth-context";
import { ApiError } from "../lib/api";

function Field({
  id,
  label,
  hint,
  ...input
}: { id: string; label: string; hint?: string } & React.InputHTMLAttributes<HTMLInputElement>) {
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

function AuthShell({ title, children, footer }: { title: string; children: ReactNode; footer: ReactNode }) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-14 sm:px-6">
      <div className="max-w-sm">
        <h1 className="marquee text-6xl">{title}</h1>
        {children}
        <p className="mt-6 text-sm text-haze">{footer}</p>
      </div>
    </div>
  );
}

function FormError({ error }: { error: unknown }) {
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

const submitClass =
  "w-full rounded-sm bg-projector px-4 py-2.5 font-semibold text-salon transition hover:brightness-110 disabled:opacity-60";

export function LoginPage() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const from = (useLocation().state as { from?: string } | null)?.from ?? "/";
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  if (user) {
    return <Navigate to={from} replace />;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setPending(true);
    setError(null);

    try {
      await login(String(form.get("userInput")), String(form.get("password")));
      navigate(from, { replace: true });
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 401
          ? new Error("Wrong username, email or password.")
          : err,
      );
    } finally {
      setPending(false);
    }
  }

  return (
    <AuthShell
      title="Log in"
      footer={
        <>
          New to Reelr?{" "}
          <Link to="/register" className="font-medium text-projector underline underline-offset-4">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={submit} className="mt-8 space-y-5">
        <Field id="userInput" label="Username or email" autoComplete="username" minLength={3} />
        <Field id="password" label="Password" type="password" autoComplete="current-password" minLength={8} />
        <FormError error={error} />
        <button type="submit" disabled={pending} className={submitClass}>
          {pending ? "Logging in…" : "Log in"}
        </button>
      </form>
    </AuthShell>
  );
}

export function RegisterPage() {
  const { user, register } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  if (user) {
    return <Navigate to="/" replace />;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setPending(true);
    setError(null);

    try {
      await register({
        username: String(form.get("username")),
        email: String(form.get("email")),
        password: String(form.get("password")),
      });
      navigate("/", { replace: true });
    } catch (err) {
      setError(err);
    } finally {
      setPending(false);
    }
  }

  return (
    <AuthShell
      title="Create account"
      footer={
        <>
          Already have an account?{" "}
          <Link to="/login" className="font-medium text-projector underline underline-offset-4">
            Log in
          </Link>
        </>
      }
    >
      <form onSubmit={submit} className="mt-8 space-y-5">
        <Field id="username" label="Username" autoComplete="username" minLength={3} maxLength={50} />
        <Field id="email" label="Email" type="email" autoComplete="email" />
        <Field
          id="password"
          label="Password"
          type="password"
          autoComplete="new-password"
          minLength={8}
          hint="At least 8 characters, with an uppercase letter, a lowercase letter, a digit and a symbol."
        />
        <FormError error={error} />
        <button type="submit" disabled={pending} className={submitClass}>
          {pending ? "Creating account…" : "Create account"}
        </button>
      </form>
    </AuthShell>
  );
}
