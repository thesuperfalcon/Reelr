import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { describe, expect, it } from "vitest";
import { FilmActions } from "../components/FilmActions";
import type { MovieDetails, Status } from "../lib/types";
import { json, mockFetch, requestsOf } from "./fakes";

const movie = { id: 7, title: "Past Lives", releaseDate: "2023-06-02", posterPath: null } as MovieDetails;
const watchedStatus: Status = { tmdbId: 7, liked: null, rewatched: false, watchedAt: "2026-10-01T12:00:00Z" };

// The film's saved state. Writes answer with what the API would send back.
function fakeApi({ status = null as Status | null, score = null as number | null } = {}) {
  return mockFetch({
    "GET /api/movies/7/status": () => (status ? json(status) : new Response(null, { status: 404 })),
    "GET /api/movies/7/rating": () => (score === null ? new Response(null, { status: 404 }) : json({ tmdbId: 7, score })),
    "GET /api/movies/7/reviews/mine": () => new Response(null, { status: 204 }),
    "GET /api/watchlist/7": () => json({ onWatchlist: false }),
    "POST /api/movies/7/status": async (request) => {
      status = { ...watchedStatus, ...(await request.json()) };
      return json(status);
    },
    "PUT /api/movies/7/status": async (request) => {
      status = { ...status!, ...(await request.json()) };
      return json(status);
    },
    "DELETE /api/movies/7/status": () => {
      status = null;
      return new Response(null, { status: 204 });
    },
    "POST /api/movies/7/rating": async (request) => {
      score = (await request.json()).score;
      return json({ tmdbId: 7, score });
    },
    "PUT /api/movies/7/rating": async (request) => {
      score = (await request.json()).score;
      return json({ tmdbId: 7, score });
    },
  });
}

function renderActions() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <FilmActions movie={movie} />
    </QueryClientProvider>,
  );
}

async function bodiesOf(fetchMock: ReturnType<typeof mockFetch>, method: string, path: string) {
  const matching = requestsOf(fetchMock).filter((r) => r.method === method && new URL(r.url).pathname === path);
  return Promise.all(matching.map((r) => r.json()));
}

describe("film actions", () => {
  it("mark an unwatched film watched in one click", async () => {
    const fetchMock = fakeApi();
    renderActions();
    const user = userEvent.setup();
    const watched = await screen.findByRole("button", { name: "Watched", pressed: false });

    await waitFor(() => expect(watched).toBeEnabled());
    await user.click(watched);

    await screen.findByRole("button", { name: "Watched", pressed: true });
    expect(await bodiesOf(fetchMock, "POST", "/api/movies/7/status")).toEqual([{ liked: null, rewatched: false }]);
  });

  it("like an unwatched film by marking it watched and liked", async () => {
    const fetchMock = fakeApi();
    renderActions();
    const user = userEvent.setup();
    const like = await screen.findByRole("button", { name: "Liked" });

    await waitFor(() => expect(like).toBeEnabled());
    await user.click(like);

    await screen.findByRole("button", { name: "Liked", pressed: true });
    expect(await bodiesOf(fetchMock, "POST", "/api/movies/7/status")).toEqual([{ liked: true, rewatched: false }]);
  });

  it("change the like of a watched film with an update, not a new log", async () => {
    const fetchMock = fakeApi({ status: watchedStatus });
    renderActions();
    const user = userEvent.setup();
    const like = await screen.findByRole("button", { name: "Liked", pressed: false });

    await waitFor(() => expect(like).toBeEnabled());
    await user.click(like);

    expect(screen.getByRole("button", { name: "Liked", pressed: true })).toBeInTheDocument();
    await waitFor(async () =>
      expect(await bodiesOf(fetchMock, "PUT", "/api/movies/7/status")).toEqual([{ liked: true, rewatched: false }]),
    );
    expect(await bodiesOf(fetchMock, "POST", "/api/movies/7/status")).toEqual([]);
  });

  it("save the stars once after the arrow keys stop", async () => {
    const fetchMock = fakeApi({ status: watchedStatus, score: 3 });
    renderActions();
    const user = userEvent.setup();
    const stars = await screen.findByRole("slider", { name: "Your rating" });

    stars.focus();
    await user.keyboard("{ArrowRight}{ArrowRight}");

    expect(stars).toHaveAttribute("aria-valuetext", "4 out of 5 stars");
    await waitFor(async () => expect(await bodiesOf(fetchMock, "PUT", "/api/movies/7/rating")).toEqual([{ score: 4 }]));
    expect(await bodiesOf(fetchMock, "POST", "/api/movies/7/rating")).toEqual([]);
  });

  it("rate an unrated film with its first rating", async () => {
    const fetchMock = fakeApi();
    renderActions();
    const user = userEvent.setup();
    const stars = await screen.findByRole("slider", { name: "Your rating" });

    stars.focus();
    await user.keyboard("{End}");

    await waitFor(async () => expect(await bodiesOf(fetchMock, "POST", "/api/movies/7/rating")).toEqual([{ score: 5 }]));
  });

  it("ask before removing a watched film, and put focus back on Watched", async () => {
    const fetchMock = fakeApi({ status: watchedStatus });
    renderActions();
    const user = userEvent.setup();
    const watched = await screen.findByRole("button", { name: "Watched", pressed: true });

    await waitFor(() => expect(watched).toBeEnabled());
    await user.click(watched);

    expect(screen.getByRole("button", { name: "Keep" })).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(watched).toHaveFocus();
    expect(requestsOf(fetchMock).some((r) => r.method === "DELETE")).toBe(false);

    await user.click(watched);
    await user.click(screen.getByRole("button", { name: "Remove" }));

    await screen.findByRole("button", { name: "Watched", pressed: false });
    expect(screen.queryByRole("button", { name: "Keep" })).not.toBeInTheDocument();
  });

  it("pass axe, with the removal question open too", async () => {
    fakeApi({ status: { ...watchedStatus, liked: true }, score: 4 });
    const { container } = renderActions();
    const user = userEvent.setup();
    const watched = await screen.findByRole("button", { name: "Watched", pressed: true });
    await waitFor(() => expect(watched).toBeEnabled());

    await user.click(watched);

    const results = await axe.run(container, {
      runOnly: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
      rules: { "color-contrast": { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
