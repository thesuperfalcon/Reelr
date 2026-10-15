import { describe, expect, it, vi } from "vitest";
import { fakeToken, json, mockFetch, requestsOf } from "../test/fakes";
import { ApiError, api, getToken, refreshAccessToken, setToken, setUnauthorizedHandler, wasSignedIn } from "./api";

describe("api", () => {
  it("sends the access token and JSON headers", async () => {
    const fetchMock = mockFetch({ "POST /api/lists": () => json({ id: 1 }) });
    setToken("access-1");

    const result = await api<{ id: number }>("/api/lists", { method: "POST", body: JSON.stringify({ name: "A" }) });

    expect(result).toEqual({ id: 1 });
    const [request] = requestsOf(fetchMock);
    expect(request.headers.get("Authorization")).toBe("Bearer access-1");
    expect(request.headers.get("Content-Type")).toBe("application/json");
    expect(request.headers.get("Accept")).toBe("application/json");
  });

  it("leaves Content-Type to the browser for form uploads", async () => {
    const fetchMock = mockFetch({ "PUT /api/settings/avatar": () => json({}) });

    await api("/api/settings/avatar", { method: "PUT", body: new FormData() });

    expect(requestsOf(fetchMock)[0].headers.get("Content-Type")).toMatch(/^multipart\/form-data/);
  });

  it("returns undefined for 204 No Content", async () => {
    mockFetch({ "DELETE /api/lists/1": () => new Response(null, { status: 204 }) });

    await expect(api("/api/lists/1", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("turns a validation problem into an ApiError with every message", async () => {
    mockFetch({
      "POST /auth/register": () =>
        json({ title: "One or more validation errors occurred.", errors: { Password: ["Too short.", "Needs a digit."], Username: ["Taken."] } }, 400),
    });

    const error = await api("/auth/register", { method: "POST", body: "{}" }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toBe("One or more validation errors occurred.");
    expect((error as ApiError).details).toEqual(["Too short.", "Needs a digit.", "Taken."]);
  });

  it("uses a plain text body as the message", async () => {
    mockFetch({ "POST /api/watchlist/5": () => new Response("Movie is already on the watchlist.", { status: 409 }) });

    await expect(api("/api/watchlist/5", { method: "POST" })).rejects.toMatchObject({
      status: 409,
      message: "Movie is already on the watchlist.",
    });
  });

  it("refreshes a token the API refused and tries again once", async () => {
    let calls = 0;
    const fetchMock = mockFetch({
      "GET /api/settings": (request) =>
        request.headers.get("Authorization") === "Bearer fresh" ? json({ userName: "cleo" }) : new Response(null, { status: 401 }),
      "POST /auth/refresh": () => {
        calls++;
        return json({ token: "fresh" });
      },
    });
    setToken("expired");

    await expect(api("/api/settings")).resolves.toEqual({ userName: "cleo" });

    expect(calls).toBe(1);
    expect(getToken()).toBe("fresh");
    const refresh = requestsOf(fetchMock).find((r) => r.url.endsWith("/auth/refresh"))!;
    expect(refresh.headers.get("X-Reelr-Csrf")).toBe("1");
  });

  it("ends the session when the sign-in itself is over", async () => {
    mockFetch({
      "GET /api/settings": () => new Response(null, { status: 401 }),
      "POST /auth/refresh": () => new Response(null, { status: 401 }),
    });
    const unauthorized = vi.fn();
    setUnauthorizedHandler(unauthorized);
    setToken("revoked");

    await expect(api("/api/settings")).rejects.toMatchObject({ status: 401 });

    expect(unauthorized).toHaveBeenCalledOnce();
    expect(getToken()).toBeNull();
    expect(wasSignedIn()).toBe(false);
    setUnauthorizedHandler(null);
  });

  it("keeps the sign-in when the refresh gets no answer", async () => {
    mockFetch({
      "GET /api/settings": () => new Response(null, { status: 401 }),
      "POST /auth/refresh": () => Promise.reject(new TypeError("Load failed")),
    });
    const unauthorized = vi.fn();
    setUnauthorizedHandler(unauthorized);
    setToken("expired");

    await expect(api("/api/settings")).rejects.toThrow(TypeError);

    expect(unauthorized).not.toHaveBeenCalled();
    expect(getToken()).toBe("expired");
    expect(localStorage.getItem("reelr.signedIn")).toBe("1");
    setUnauthorizedHandler(null);
  });

  it("does not refresh for a visitor who is not signed in", async () => {
    const fetchMock = mockFetch({ "GET /api/settings": () => new Response(null, { status: 401 }) });

    await expect(api("/api/settings")).rejects.toMatchObject({ status: 401 });

    expect(requestsOf(fetchMock).map((r) => new URL(r.url).pathname)).toEqual(["/api/settings"]);
  });
});

describe("refreshAccessToken", () => {
  it("shares one request between callers", async () => {
    let calls = 0;
    mockFetch({
      "POST /auth/refresh": async () => {
        calls++;
        await new Promise((resolve) => setTimeout(resolve, 10));
        return json({ token: fakeToken(1, "cleo") });
      },
    });

    const [first, second] = await Promise.all([refreshAccessToken(), refreshAccessToken()]);

    expect(calls).toBe(1);
    expect(first).toBe(second);
  });

  it("remembers that this browser is signed in, without storing the token", async () => {
    const token = fakeToken(1, "cleo");
    mockFetch({ "POST /auth/refresh": () => json({ token }) });
    localStorage.setItem("reelr.token", "old-stored-token");

    await refreshAccessToken();

    expect(wasSignedIn()).toBe(true);
    expect(Object.values(localStorage)).not.toContain(token);
    expect(localStorage.getItem("reelr.token")).toBeNull();
  });

  it("resolves to null and forgets the sign-in when the cookie is no longer valid", async () => {
    mockFetch({ "POST /auth/refresh": () => new Response(null, { status: 401 }) });
    localStorage.setItem("reelr.signedIn", "1");

    await expect(refreshAccessToken()).resolves.toBeNull();

    expect(wasSignedIn()).toBe(false);
  });
});
