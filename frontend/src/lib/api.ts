const TOKEN_KEY = "reelr.token";

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setToken(token: string | null) {
  try {
    if (token) {
      localStorage.setItem(TOKEN_KEY, token);
    } else {
      localStorage.removeItem(TOKEN_KEY);
    }
  } catch {
    // Storage can be blocked (private mode). The session then lasts until reload.
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

// Called when the API rejects the stored token, so the auth state can reset.
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

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(path, { ...init, headers });

  if (response.status === 401 && token) {
    onUnauthorized?.();
  }

  if (!response.ok) {
    throw await readError(response);
  }

  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return undefined as T;
  }

  return (await response.json()) as T;
}
