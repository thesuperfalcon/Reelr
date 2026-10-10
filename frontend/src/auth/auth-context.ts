import { createContext, useContext } from "react";
import type { CurrentUser } from "../lib/types";

export interface RegisterInput {
  username: string;
  email: string;
  password: string;
}

/** "expiring": the session ends in a few minutes. "expired": it ended, or the API refused the token. */
export type SessionNotice = "expiring" | "expired" | null;

export interface AuthContextValue {
  user: CurrentUser | null;
  login: (userInput: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
  /** Swaps in a new token for the same user, e.g. after a rename, so the shown name stays right. */
  replaceToken: (token: string) => void;
  sessionNotice: SessionNotice;
  dismissSessionNotice: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }
  return value;
}
