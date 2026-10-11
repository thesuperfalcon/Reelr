import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, getToken, setToken, setUnauthorizedHandler } from "../lib/api";
import type { CurrentUser } from "../lib/types";
import { AuthContext, type AuthContextValue, type RegisterInput, type SessionNotice } from "./auth-context";

// The backend writes ClaimTypes.Name as "unique_name" in the JWT.
interface TokenPayload {
  sub?: string;
  unique_name?: string;
  exp?: number;
}

// The session warning shows this long before the token runs out.
const WARN_BEFORE_MS = 5 * 60 * 1000;

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

  if (!payload?.sub || (payload.exp && payload.exp * 1000 < Date.now())) {
    return null;
  }

  return { id: Number(payload.sub), username: payload.unique_name ?? "" };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<CurrentUser | null>(() => {
    const stored = userFromToken(getToken());
    // A token that ran out while the site was closed is dropped quietly, without the "session ended" notice.
    if (!stored) {
      setToken(null);
    }
    return stored;
  });
  const [sessionNotice, setSessionNotice] = useState<SessionNotice>(null);

  const signOut = useCallback(
    (notice: SessionNotice) => {
      setToken(null);
      setUser(null);
      setSessionNotice(notice);
      queryClient.clear();
    },
    [queryClient],
  );

  const logout = useCallback(() => signOut(null), [signOut]);

  // The API refused the token (it ran out, or the account is gone), so the user is told instead of
  // silently finding themselves logged out.
  const expire = useCallback(() => signOut("expired"), [signOut]);

  useEffect(() => {
    setUnauthorizedHandler(expire);
    return () => setUnauthorizedHandler(null);
  }, [expire]);

  // Warns a few minutes before the token runs out, and ends the session when it does, so nobody keeps
  // writing in a session that can no longer save.
  useEffect(() => {
    const expiresAt = (payloadOf(getToken())?.exp ?? 0) * 1000;

    if (!user || !expiresAt) {
      return;
    }

    const warning = setTimeout(() => setSessionNotice("expiring"), Math.max(0, expiresAt - WARN_BEFORE_MS - Date.now()));
    const end = setTimeout(expire, Math.max(0, expiresAt - Date.now()));

    return () => {
      clearTimeout(warning);
      clearTimeout(end);
    };
  }, [user, expire]);

  const dismissSessionNotice = useCallback(() => setSessionNotice(null), []);

  const login = useCallback(async (userInput: string, password: string) => {
    const { token } = await api<{ token: string }>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ userInput, password }),
    });
    setToken(token);
    setUser(userFromToken(token));
    setSessionNotice(null);
    // Profiles fetched while logged out say isFollowing: false, and the logged-out
    // community feed still contains the user's own activity.
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["users"] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
  }, [queryClient]);

  // The new account can log in once its email is confirmed, so registering does not log in.
  const register = useCallback(async (input: RegisterInput) => {
    await api("/auth/register", { method: "POST", body: JSON.stringify(input) });
  }, []);

  const replaceToken = useCallback((token: string) => {
    setToken(token);
    setUser(userFromToken(token));
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, login, register, logout, replaceToken, sessionNotice, dismissSessionNotice }),
    [user, login, register, logout, replaceToken, sessionNotice, dismissSessionNotice],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
