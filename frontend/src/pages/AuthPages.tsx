import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { Field, FormError } from "../components/Form";
import { clearEmailLink, confirmEmail, forgotPassword, readEmailLink, resendConfirmation, resetPassword } from "../lib/account";
import { ApiError } from "../lib/api";
import { usePageTitle } from "../lib/title";

function AuthShell({ title, children, footer }: { title: string; children: ReactNode; footer?: ReactNode }) {
  return (
    <div className="mx-auto max-w-6xl px-4 py-14 sm:px-6">
      <div className="max-w-sm">
        <h1 className="marquee text-6xl">{title}</h1>
        {children}
        {footer && <p className="mt-6 text-sm text-haze">{footer}</p>}
      </div>
    </div>
  );
}

// The API limits auth calls per client and answers 429 when the limit is hit.
function rateLimitedOr(err: unknown): unknown {
  return err instanceof ApiError && err.status === 429
    ? new Error("Too many attempts. Try again later.")
    : err;
}

const submitClass =
  "w-full rounded-sm bg-projector px-4 py-2.5 font-semibold text-salon transition hover:brightness-110 disabled:opacity-60";

const linkClass = "font-medium text-projector underline underline-offset-4";

const passwordHint = "At least 8 characters, with an uppercase letter, a lowercase letter, a digit and a symbol.";

// "Check your inbox", with a way to have the confirmation email sent again.
function CheckYourEmail({ email, children }: { email: string; children: ReactNode }) {
  const [state, setState] = useState<"idle" | "sending" | "sent">("idle");
  const [error, setError] = useState<unknown>(null);

  async function resend() {
    setState("sending");
    setError(null);
    try {
      await resendConfirmation(email);
      setState("sent");
    } catch (err) {
      setError(rateLimitedOr(err));
      setState("idle");
    }
  }

  return (
    <div role="status" className="mt-8 space-y-4">
      {children}
      <p className="text-sm text-haze">
        No email after a few minutes? Check the spam folder, or{" "}
        <button type="button" onClick={resend} disabled={state !== "idle"} className={linkClass}>
          {state === "sending" ? "sending…" : state === "sent" ? "sent again" : "send it again"}
        </button>
        .
      </p>
      <FormError error={error} />
    </div>
  );
}

export function LoginPage() {
  usePageTitle("Log in");
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const from = (useLocation().state as { from?: string } | null)?.from ?? "/";
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);
  // Set when the password was right but the email is not confirmed yet.
  const [unconfirmed, setUnconfirmed] = useState(false);
  const [email, setEmail] = useState("");

  if (user) {
    return <Navigate to={from} replace />;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const userInput = String(form.get("userInput"));
    setPending(true);
    setError(null);

    try {
      await login(userInput, String(form.get("password")));
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.status === 403) {
        setUnconfirmed(true);
        setEmail(userInput.includes("@") ? userInput : "");
      } else {
        setError(
          err instanceof ApiError && err.status === 401
            ? new Error("Wrong username, email or password.")
            : rateLimitedOr(err),
        );
      }
    } finally {
      setPending(false);
    }
  }

  if (unconfirmed) {
    return (
      <AuthShell title="Confirm your email">
        <p className="mt-8 text-sm">
          Your account is almost ready. Open the link in the email we sent when you signed up, then log in.
        </p>
        <ResendForm initialEmail={email} />
        <p className="mt-6 text-sm">
          <button type="button" onClick={() => setUnconfirmed(false)} className={linkClass}>
            Back to log in
          </button>
        </p>
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title="Log in"
      footer={
        <>
          New to Reelr?{" "}
          <Link to="/register" className={linkClass}>
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
      <p className="mt-4 text-sm">
        <Link to="/forgot-password" className={linkClass}>
          Forgot your password?
        </Link>
      </p>
    </AuthShell>
  );
}

// Asks for the address to send the confirmation email to again.
function ResendForm({ initialEmail }: { initialEmail: string }) {
  const [email, setEmail] = useState(initialEmail);
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setError(null);
    try {
      await resendConfirmation(email);
      setSentTo(email);
    } catch (err) {
      setError(rateLimitedOr(err));
    } finally {
      setPending(false);
    }
  }

  if (sentTo) {
    return (
      <p role="status" className="mt-6 text-sm">
        If <span className="font-semibold">{sentTo}</span> belongs to an account that is not confirmed yet, a new link is on its way.
      </p>
    );
  }

  return (
    <form onSubmit={submit} className="mt-6 space-y-5">
      <Field
        id="resend-email"
        label="Email to send the link to again"
        type="email"
        autoComplete="email"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
      />
      <FormError error={error} />
      <button type="submit" disabled={pending} className={submitClass}>
        {pending ? "Sending…" : "Send the link again"}
      </button>
    </form>
  );
}

export function RegisterPage() {
  usePageTitle("Create account");
  const { user, register } = useAuth();
  const [searchParams] = useSearchParams();
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);
  const [sentTo, setSentTo] = useState<string | null>(null);

  if (user) {
    return <Navigate to="/" replace />;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email"));
    setPending(true);
    setError(null);

    try {
      await register({
        username: String(form.get("username")),
        email,
        password: String(form.get("password")),
        acceptTerms: form.get("acceptTerms") === "on",
        inviteCode: String(form.get("inviteCode")).trim() || undefined,
      });
      setSentTo(email);
    } catch (err) {
      setError(rateLimitedOr(err));
    } finally {
      setPending(false);
    }
  }

  if (sentTo) {
    return (
      <AuthShell title="Check your inbox">
        <CheckYourEmail email={sentTo}>
          <p className="text-sm">
            We sent a link to <span className="font-semibold">{sentTo}</span>. Open it to confirm your address, then log in.
          </p>
        </CheckYourEmail>
      </AuthShell>
    );
  }

  return (
    <AuthShell
      title="Create account"
      footer={
        <>
          Already have an account?{" "}
          <Link to="/login" className={linkClass}>
            Log in
          </Link>
        </>
      }
    >
      <form onSubmit={submit} className="mt-8 space-y-5">
        {/* Filled in from the invite link (/register?invite=…); otherwise typed or pasted from the invite. */}
        <Field
          id="inviteCode"
          label="Invite code"
          autoComplete="off"
          spellCheck={false}
          required={false}
          maxLength={64}
          defaultValue={searchParams.get("invite") ?? ""}
          hint="Reelr is invite only for now. The code is in the invite you were sent."
        />
        <Field id="username" label="Username" autoComplete="username" minLength={3} maxLength={50} />
        <Field id="email" label="Email" type="email" autoComplete="email" />
        <Field id="password" label="Password" type="password" autoComplete="new-password" minLength={8} hint={passwordHint} />
        <label className="flex items-start gap-3 text-sm">
          <input type="checkbox" name="acceptTerms" required className="mt-0.5 size-4 accent-projector" />
          <span>
            I am at least 13 and accept the{" "}
            <Link to="/terms" target="_blank" className={linkClass}>
              terms
            </Link>{" "}
            and the{" "}
            <Link to="/privacy" target="_blank" className={linkClass}>
              privacy policy
            </Link>
            .
          </span>
        </label>
        <FormError error={error} />
        <button type="submit" disabled={pending} className={submitClass}>
          {pending ? "Creating account…" : "Create account"}
        </button>
      </form>
    </AuthShell>
  );
}

// Opened from the link in the confirmation email.
export function ConfirmEmailPage() {
  usePageTitle("Confirm email");
  const [link] = useState(readEmailLink);
  const [state, setState] = useState<"confirming" | "done" | "failed">(link ? "confirming" : "failed");
  const [error, setError] = useState<unknown>(null);
  // StrictMode runs effects twice in development; the link only needs to be sent once.
  const sent = useRef(false);

  useEffect(() => {
    clearEmailLink();

    if (!link || sent.current) {
      return;
    }

    sent.current = true;
    confirmEmail(link.userId, link.token)
      .then(() => setState("done"))
      .catch((err) => {
        setError(err);
        setState("failed");
      });
  }, [link]);

  return (
    <AuthShell title={state === "done" ? "Email confirmed" : "Confirm email"}>
      <div role="status" className="mt-8 space-y-4 text-sm">
        {state === "confirming" && <p>Confirming your email…</p>}
        {state === "done" && (
          <p>
            Your account is ready.{" "}
            <Link to="/login" className={linkClass}>
              Log in
            </Link>
          </p>
        )}
        {state === "failed" && (
          <>
            <FormError error={error ?? new Error("This link is incomplete. Open it straight from the email.")} />
            <p className="text-haze">
              Links work for 3 hours. You can ask for a new one from the{" "}
              <Link to="/login" className={linkClass}>
                log in page
              </Link>
              .
            </p>
          </>
        )}
      </div>
    </AuthShell>
  );
}

export function ForgotPasswordPage() {
  usePageTitle("Forgot password");
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const email = String(new FormData(event.currentTarget).get("email"));
    setPending(true);
    setError(null);

    try {
      await forgotPassword(email);
      setSentTo(email);
    } catch (err) {
      setError(rateLimitedOr(err));
    } finally {
      setPending(false);
    }
  }

  return (
    <AuthShell
      title="Forgot password"
      footer={
        <Link to="/login" className={linkClass}>
          Back to log in
        </Link>
      }
    >
      {sentTo ? (
        <p role="status" className="mt-8 text-sm">
          If <span className="font-semibold">{sentTo}</span> belongs to a Reelr account, we sent it a link for choosing a new
          password. The link works for 3 hours.
        </p>
      ) : (
        <form onSubmit={submit} className="mt-8 space-y-5">
          <p className="text-sm text-haze">Enter the email address of your account and we will send you a link.</p>
          <Field id="email" label="Email" type="email" autoComplete="email" />
          <FormError error={error} />
          <button type="submit" disabled={pending} className={submitClass}>
            {pending ? "Sending…" : "Send the link"}
          </button>
        </form>
      )}
    </AuthShell>
  );
}

// Opened from the link in the reset email.
export function ResetPasswordPage() {
  usePageTitle("Choose a new password");
  const [link] = useState(readEmailLink);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    clearEmailLink();
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const password = String(form.get("password"));

    if (password !== String(form.get("confirmPassword"))) {
      setError(new Error("The passwords do not match."));
      return;
    }

    setPending(true);
    setError(null);

    try {
      await resetPassword(link!.userId, link!.token, password);
      setDone(true);
    } catch (err) {
      setError(rateLimitedOr(err));
    } finally {
      setPending(false);
    }
  }

  if (!link) {
    return (
      <AuthShell title="Reset password">
        <p className="mt-8 text-sm">
          This link is incomplete. Open it straight from the email, or{" "}
          <Link to="/forgot-password" className={linkClass}>
            ask for a new one
          </Link>
          .
        </p>
      </AuthShell>
    );
  }

  if (done) {
    return (
      <AuthShell title="Password changed">
        <p role="status" className="mt-8 text-sm">
          Your new password is set, and you have been logged out on every device.{" "}
          <Link to="/login" className={linkClass}>
            Log in
          </Link>
        </p>
      </AuthShell>
    );
  }

  return (
    <AuthShell title="New password">
      <form onSubmit={submit} className="mt-8 space-y-5">
        <Field id="password" label="New password" type="password" autoComplete="new-password" minLength={8} hint={passwordHint} />
        <Field id="confirmPassword" label="New password again" type="password" autoComplete="new-password" minLength={8} />
        <FormError error={error} />
        <button type="submit" disabled={pending} className={submitClass}>
          {pending ? "Saving…" : "Set new password"}
        </button>
      </form>
      {error instanceof ApiError && error.status === 400 && error.details.length === 0 && (
        <p className="mt-4 text-sm">
          <Link to="/forgot-password" className={linkClass}>
            Ask for a new link
          </Link>
        </p>
      )}
    </AuthShell>
  );
}
