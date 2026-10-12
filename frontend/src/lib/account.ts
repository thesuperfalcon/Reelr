import { ApiError, api, getToken, refreshAccessToken } from "./api";

// The account email flows. Every request that sends an email answers the same whether or not the address
// has an account, so these only report errors such as rate limits or an invalid link.

export function resendConfirmation(email: string) {
  return api<void>("/auth/resend-confirmation", { method: "POST", body: JSON.stringify({ email }) });
}

export function forgotPassword(email: string) {
  return api<void>("/auth/forgot-password", { method: "POST", body: JSON.stringify({ email }) });
}

export function confirmEmail(userId: number, token: string) {
  return api<void>("/auth/confirm-email", { method: "POST", body: JSON.stringify({ userId, token }) });
}

export function resetPassword(userId: number, token: string, newPassword: string) {
  return api<void>("/auth/reset-password", { method: "POST", body: JSON.stringify({ userId, token, newPassword }) });
}

// The links in account emails carry "#userId=…&token=…". The part after "#" never reaches a server.
export function readEmailLink(): { userId: number; token: string } | null {
  const params = new URLSearchParams(window.location.hash.slice(1));
  const userId = Number(params.get("userId"));
  const token = params.get("token");

  return Number.isInteger(userId) && userId > 0 && token ? { userId, token } : null;
}

// Removes the token from the address bar once it has been read, so it does not stay in the browser history.
export function clearEmailLink() {
  if (window.location.hash) {
    window.history.replaceState(window.history.state, "", window.location.pathname + window.location.search);
  }
}

// Downloads everything Reelr stores about the user as a JSON file. The request needs the login token,
// so a plain link would not work; the file is fetched and then handed to the browser to save.
export async function downloadDataExport() {
  const request = (token: string | null) =>
    fetch("/api/settings/export", { headers: { Authorization: `Bearer ${token ?? ""}` } });

  let response = await request(getToken());
  if (response.status === 401) {
    response = await request(await refreshAccessToken().catch(() => null));
  }

  if (!response.ok) {
    throw new ApiError(response.status, "Could not create the download. Try again in a moment.");
  }

  const name = /filename="?([^";]+)"?/.exec(response.headers.get("Content-Disposition") ?? "")?.[1] ?? "reelr-export.json";
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.click();
  URL.revokeObjectURL(url);
}
