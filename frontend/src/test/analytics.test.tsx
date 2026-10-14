import { render, waitFor } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";
import { routeOf, usePageViews } from "../lib/analytics";
import { mockFetch, requestsOf } from "./fakes";

describe("routeOf", () => {
  it("names the ids instead of sending them", () => {
    expect(routeOf("/user/42", { userId: "42" })).toBe("/user/:userId");
    expect(routeOf("/movie/550/", { tmdbId: "550" })).toBe("/movie/:tmdbId");
    expect(routeOf("/", {})).toBe("/");
    expect(routeOf("/search", {})).toBe("/search");
  });

  it("sends unknown addresses as the not found page", () => {
    expect(routeOf("/no/such/page", { "*": "no/such/page" })).toBe("/*");
  });
});

function Counted() {
  usePageViews();
  return null;
}

function renderAt(path: string) {
  const router = createMemoryRouter([{ path: "/review/:reviewId", element: <Counted /> }], { initialEntries: [path] });
  render(<RouterProvider router={router} />);
}

describe("usePageViews", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("counts the route of the page that is shown", async () => {
    const fetchMock = mockFetch({ "POST /api/analytics/page-views": () => new Response(null, { status: 204 }) });

    renderAt("/review/7");

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(await requestsOf(fetchMock)[0].json()).toEqual({ route: "/review/:reviewId" });
  });

  it("counts nothing when the browser asks not to be tracked", () => {
    vi.stubGlobal("navigator", { ...navigator, doNotTrack: "1" });
    const fetchMock = mockFetch({});

    renderAt("/review/7");

    expect(fetchMock).not.toHaveBeenCalled();
  });
});
