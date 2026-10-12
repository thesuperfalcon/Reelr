import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
import { setToken } from "../lib/api";

afterEach(() => {
  cleanup();
  // The access token is module state, so each test starts signed out.
  setToken(null);
  localStorage.clear();
});
