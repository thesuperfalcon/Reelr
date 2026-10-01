import { createContext, useContext } from "react";
import type { CurrentUser } from "../lib/types";

export interface RegisterInput {
  username: string;
  email: string;
  password: string;
}

export interface AuthContextValue {
  user: CurrentUser | null;
  login: (userInput: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }
  return value;
}
