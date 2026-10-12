import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, refreshAccessToken, revokeRefreshToken, setToken, setUnauthorizedHandler, wasSignedIn } from "../lib/api";
import { Loading } from "../components/Status";
import type { CurrentUser } from "../lib/types";
import { AuthContext, type AuthContextValue, type RegisterInput, type SessionNotice } from "./auth-context";

// The backend writes ClaimTypes.Name as "unique_name" in the JWT.
interface TokenPayload {
  sub?: string;
  unique_name?: string;
  exp?: number;
}

// A new access token is fetched this long before the current one runs out, but never before half its
// remaining time has passed, so a very short token cannot cause a stream of refreshes.
const REFRESH_BEFORE_MS = 60 * 1000;

function payloadOf(token: string | null): TokenPayload | null {
  if (!token) {
    return null;
  }

  try {
    const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(base64)) as TokenPayload;
  } catch {
    return null;
  }
}

function userFromToken(token: string | null): CurrentUser | null {
  const payload = payloadOf(token);

  if (!payload?.sub) {
    return null;
  }

  return { id: Number(payload.sub), username: payload.unique_name ?? "" };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  // The access token is only in memory, so a page load starts by asking the refresh cookie for one.
  const [restoring, setRestoring] = useState(wasSignedIn);
  const [token, setTokenState] = useState<string | null>(null);
  const [sessionNotice, setSessionNotice] = useState<SessionNotice>(null);
  const user = useMemo(() => userFromToken(token), [token]);

  const applyToken = useCallback((next: string | null) => {
    setToken(next);
    setTokenState(next);
  }, []);

  const signOut = useCallback(
    (notice: SessionNotice) => {
      applyToken(null);
      setSessionNotice(notice);
      queryClient.clear();
    },
    [queryClient, applyToken],
  );

  const logout = useCallback(() => {
    void revokeRefreshToken();
    signOut(null);
  }, [signOut]);

  // The sign-in has ended (logged out elsewhere, password changed, account gone), so the user is told
  // instead of silently finding themselves logged out.
  const expire = useCallback(() => signOut("expired"), [signOut]);

  useEffect(() => {
    setUnauthorizedHandler(expire);
    return () => setUnauthorizedHandler(null);
  }, [expire]);

  useEffect(() => {
    if (!restoring) {
      return;
    }

    refreshAccessToken()
      .then((fresh) => setTokenState(fresh))
      // Offline: the page works logged out for now, and the next load tries again.
      .catch(() => undefined)
      .finally(() => setRestoring(false));
  }, [restoring]);

  // Gets a new access token shortly before the current one runs out, so a long visit never hits a 401.
  // api() also refreshes on a 401, so a timer delayed by a sleeping laptop does no harm.
  useEffect(() => {
    const expiresAt = (payloadOf(token)?.exp ?? 0) * 1000;

    if (!token || !expiresAt) {
      return;
    }

    const timer = setTimeout(() => {
      refreshAccessToken()
        .then((fresh) => (fresh ? setTokenState(fresh) : expire()))
        .catch(() => undefined);
    }, Math.max((expiresAt - Date.now()) / 2, expiresAt - REFRESH_BEFORE_MS - Date.now()));

    return () => clearTimeout(timer);
  }, [token, expire]);

  const dismissSessionNotice = useCallback(() => setSessionNotice(null), []);

  const login = useCallback(async (userInput: string, password: string) => {
    const { token: fresh } = await api<{ token: string }>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ userInput, password }),
    });
    applyToken(fresh);
    setSessionNotice(null);
    // Profiles fetched while logged out say isFollowing: false, and the logged-out
    // community feed still contains the user's own activity.
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["users"] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
  }, [queryClient, applyToken]);

  // The new account can log in once its email is confirmed, so registering does not log in.
  const register = useCallback(async (input: RegisterInput) => {
    await api("/auth/register", { method: "POST", body: JSON.stringify(input) });
  }, []);

  // The server set a new refresh cookie in the same response.
  const replaceToken = useCallback((fresh: string) => applyToken(fresh), [applyToken]);

  const value = useMemo<AuthContextValue>(
    () => ({ user, login, register, logout, replaceToken, sessionNotice, dismissSessionNotice }),
    [user, login, register, logout, replaceToken, sessionNotice, dismissSessionNotice],
  );

  // Pages wait for the restored sign-in, so a signed-in visitor is not sent to the login page first.
  return <AuthContext value={value}>{restoring ? <Loading label="Signing in" /> : children}</AuthContext>;
}
