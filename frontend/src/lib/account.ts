import { api } from "./api";

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
