// The access token lives only in memory, so it is gone when the tab closes and scripts cannot find it in
// storage. The sign-in itself is the refresh cookie, which the page's scripts cannot read at all; it gets a
// new access token on load and whenever one runs out.
let accessToken: string | null = null;

// Not a secret: only whether this browser was signed in, so a visitor who never logged in does not send a
// refresh request on every load.
const SIGNED_IN_KEY = "reelr.signedIn";
// Where tokens were kept before the refresh cookie. Removed on load.
const OLD_TOKEN_KEY = "reelr.token";

const CSRF_HEADER = "X-Reelr-Csrf";

export function getToken(): string | null {
  return accessToken;
}

export function setToken(token: string | null) {
  accessToken = token;
  try {
    localStorage.removeItem(OLD_TOKEN_KEY);
    if (token) {
      localStorage.setItem(SIGNED_IN_KEY, "1");
    } else {
      localStorage.removeItem(SIGNED_IN_KEY);
    }
  } catch {
    // Storage can be blocked (private mode). The page then tries a refresh on every load.
  }
}

export function wasSignedIn(): boolean {
  try {
    return localStorage.getItem(SIGNED_IN_KEY) !== null || localStorage.getItem(OLD_TOKEN_KEY) !== null;
  } catch {
    return true;
  }
}

let refreshing: Promise<string | null> | null = null;

// Swaps the refresh cookie for a new access token, or resolves to null when the sign-in has ended. Calls in
// this tab share one request, and tabs take turns through a Web Lock, since every refresh replaces the
// cookie and the server ends a sign-in whose old cookie comes back too late.
export function refreshAccessToken(): Promise<string | null> {
  refreshing ??= (async () => {
    const request = async () => {
      const response = await fetch("/auth/refresh", { method: "POST", headers: { [CSRF_HEADER]: "1" } });
      if (!response.ok) {
        return null;
      }
      const { token } = (await response.json()) as { token: string };
      return token;
    };

    // A network error rejects instead: the sign-in may still be fine, so it is not ended.
    try {
      const token = navigator.locks ? await navigator.locks.request("reelr-refresh", request) : await request();
      setToken(token);
      return token;
    } finally {
      refreshing = null;
    }
  })();

  return refreshing;
}

// Ends this browser's sign-in on the server. Other devices stay signed in.
export async function revokeRefreshToken() {
  setToken(null);
  try {
    await fetch("/auth/logout", { method: "POST", headers: { [CSRF_HEADER]: "1" } });
  } catch {
    // The cookie then runs out by itself.
  }
}

export class ApiError extends Error {
  readonly status: number;
  /** Field or Identity error messages from an ASP.NET ValidationProblem, flattened. */
  readonly details: string[];

  constructor(status: number, message: string, details: string[] = []) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

// Called when the API refuses the token and no new one can be had, so the auth state can reset.
let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

async function readError(response: Response): Promise<ApiError> {
  const text = await response.text();
  let message = text || response.statusText;
  let details: string[] = [];

  try {
    const body = JSON.parse(text);
    if (body && typeof body === "object") {
      message = body.title ?? message;
      if (body.errors) {
        details = Object.values(body.errors as Record<string, string[]>).flat();
      }
    } else if (typeof body === "string") {
      message = body;
    }
  } catch {
    // Plain text body, e.g. Conflict("...") or BadRequest("...").
  }

  return new ApiError(response.status, message, details);
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  // FormData sets its own multipart Content-Type with the boundary.
  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const send = (token: string | null) => {
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }
    return fetch(path, { ...init, headers });
  };

  const token = getToken();
  let response = await send(token);

  // The access token ran out or was revoked: get a new one once and try again. If the sign-in itself
  // has ended, the auth state is reset. A refresh that never got an answer (a phone dropping the request
  // while the database wakes up) rejects and is thrown as is, so the request can be retried and the
  // sign-in is kept.
  if (response.status === 401 && token) {
    const fresh = await refreshAccessToken();
    if (fresh) {
      response = await send(fresh);
    } else {
      onUnauthorized?.();
    }
  }

  if (!response.ok) {
    throw await readError(response);
  }

  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return undefined as T;
  }

  return (await response.json()) as T;
}
