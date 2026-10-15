import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { getToken } from "../lib/api";
import { fakeToken, json, mockFetch, requestsOf } from "../test/fakes";
import { AuthProvider } from "./AuthProvider";
import { useAuth } from "./auth-context";

function Who() {
  const { user, restoring, login, logout } = useAuth();
  return (
    <div>
      <p>{restoring ? "Restoring" : user ? `Signed in as ${user.username}` : "Signed out"}</p>
      <button type="button" onClick={() => login("cleo", "Passw0rd!")}>
        Log in
      </button>
      <button type="button" onClick={logout}>
        Log out
      </button>
    </div>
  );
}

function renderAuth() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider>
        <Who />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe("AuthProvider", () => {
  it("restores the session from the refresh cookie on load", async () => {
    mockFetch({ "POST /auth/refresh": () => json({ token: fakeToken(7, "cleo") }) });
    localStorage.setItem("reelr.signedIn", "1");

    renderAuth();

    expect(screen.getByText("Restoring")).toBeInTheDocument();
    expect(await screen.findByText("Signed in as cleo")).toBeInTheDocument();
  });

  it("does not ask the server for a session it never had", async () => {
    const fetchMock = mockFetch({});

    renderAuth();

    expect(await screen.findByText("Signed out")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("shows the visitor as signed out when the cookie has run out", async () => {
    mockFetch({ "POST /auth/refresh": () => new Response(null, { status: 401 }) });
    localStorage.setItem("reelr.signedIn", "1");

    renderAuth();

    expect(await screen.findByText("Signed out")).toBeInTheDocument();
  });

  it("tries the sign-in again when the page is back on screen after a dropped refresh", async () => {
    let attempts = 0;
    mockFetch({
      "POST /auth/refresh": () => {
        attempts += 1;
        if (attempts === 1) {
          // What fetch does when a phone drops the request.
          throw new TypeError("Load failed");
        }
        return json({ token: fakeToken(7, "cleo") });
      },
    });
    localStorage.setItem("reelr.signedIn", "1");

    renderAuth();
    expect(await screen.findByText("Signed out")).toBeInTheDocument();

    fireEvent(document, new Event("visibilitychange"));

    expect(await screen.findByText("Signed in as cleo")).toBeInTheDocument();
    expect(attempts).toBe(2);
  });

  it("logs in and out, ending the sign-in on the server", async () => {
    const fetchMock = mockFetch({
      "POST /auth/login": () => json({ token: fakeToken(7, "cleo") }),
      "POST /auth/logout": () => new Response(null, { status: 204 }),
    });
    renderAuth();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Log in" }));
    expect(await screen.findByText("Signed in as cleo")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Log out" }));
    expect(await screen.findByText("Signed out")).toBeInTheDocument();

    await waitFor(() => expect(requestsOf(fetchMock).some((r) => r.url.endsWith("/auth/logout"))).toBe(true));
    const logoutRequest = requestsOf(fetchMock).find((r) => r.url.endsWith("/auth/logout"))!;
    expect(logoutRequest.headers.get("X-Reelr-Csrf")).toBe("1");
    expect(getToken()).toBeNull();
    expect(localStorage.getItem("reelr.signedIn")).toBeNull();
  });
});
