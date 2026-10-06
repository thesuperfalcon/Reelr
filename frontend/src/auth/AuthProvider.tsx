import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, getToken, setToken, setUnauthorizedHandler } from "../lib/api";
import type { CurrentUser } from "../lib/types";
import { AuthContext, type AuthContextValue, type RegisterInput } from "./auth-context";

// The backend writes ClaimTypes.Name as "unique_name" in the JWT.
interface TokenPayload {
  sub?: string;
  unique_name?: string;
  exp?: number;
}

function userFromToken(token: string | null): CurrentUser | null {
  if (!token) {
    return null;
  }

  try {
    const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const payload = JSON.parse(atob(base64)) as TokenPayload;

    if (!payload.sub || (payload.exp && payload.exp * 1000 < Date.now())) {
      return null;
    }

    return { id: Number(payload.sub), username: payload.unique_name ?? "" };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<CurrentUser | null>(() => userFromToken(getToken()));

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  useEffect(() => {
    setUnauthorizedHandler(logout);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const login = useCallback(async (userInput: string, password: string) => {
    const { token } = await api<{ token: string }>("/auth/login", {
      method: "POST",
      body: JSON.stringify({ userInput, password }),
    });
    setToken(token);
    setUser(userFromToken(token));
    // Profiles fetched while logged out say isFollowing: false, and the logged-out
    // community feed still contains the user's own activity.
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["users"] }),
      queryClient.invalidateQueries({ queryKey: ["feed"] }),
    ]);
  }, [queryClient]);

  const register = useCallback(
    async (input: RegisterInput) => {
      await api("/auth/register", { method: "POST", body: JSON.stringify(input) });
      await login(input.username, input.password);
    },
    [login],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ user, login, register, logout }),
    [user, login, register, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
