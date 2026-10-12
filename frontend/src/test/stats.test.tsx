import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { DiaryStats } from "../components/DiaryStats";
import { buildWeeks, type UserStats } from "../lib/stats";
import { json, mockFetch, requestsOf } from "./fakes";

// 1 October 2026 is a Thursday and 12 October a Monday.
const stats: UserStats = {
  from: "2026-10-01",
  to: "2026-10-12",
  total: 5,
  days: [
    { date: "2026-10-03", count: 1 },
    { date: "2026-10-04", count: 2 },
    { date: "2026-10-11", count: 2 },
  ],
  longestStreak: 2,
  currentStreak: 2,
  busiestWeekday: 0,
  months: [{ year: 2026, month: 10, count: 5, averageRating: 3.5 }],
  years: [2026, 2025],
};

describe("buildWeeks", () => {
  it("lays the days out as Sunday-to-Saturday weeks with padding outside the period", () => {
    const weeks = buildWeeks(stats);

    expect(weeks).toHaveLength(3);
    // The first week starts on Sunday 27 September; the four days before 1 October are padding.
    expect(weeks[0].map((cell) => cell?.day ?? null)).toEqual([
      null,
      null,
      null,
      null,
      "2026-10-01",
      "2026-10-02",
      "2026-10-03",
    ]);
    expect(weeks[1][0]).toEqual({ day: "2026-10-04", count: 2 });
    expect(weeks[2].map((cell) => cell?.count ?? null)).toEqual([2, 0, null, null, null, null, null]);
  });

  it("counts days without films as zero", () => {
    const cells = buildWeeks(stats).flat().filter((cell) => cell != null);

    expect(cells).toHaveLength(12);
    expect(cells.reduce((sum, cell) => sum + cell.count, 0)).toBe(5);
  });
});

function renderStats(routes: Parameters<typeof mockFetch>[0]) {
  const fetchMock = mockFetch(routes);
  const router = createMemoryRouter([{ path: "/", element: <DiaryStats userId={5} own={false} /> }]);
  const view = render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <main>
        <RouterProvider router={router} />
      </main>
    </QueryClientProvider>,
  );
  return { fetchMock, ...view };
}

const dayLabel = (films: string, date: string) => new RegExp(`^${films} on .*${date}`);

// Tabs past the period buttons to the graph, which is one stop.
async function tabToGraph(user: ReturnType<typeof userEvent.setup>) {
  for (let i = 0; i < 10 && !document.activeElement?.hasAttribute("data-day"); i++) {
    await user.tab();
  }
}

describe("diary stats", () => {
  it("shows the summary and passes axe", async () => {
    const { container } = renderStats({ "GET /api/users/5/stats": () => json(stats) });

    expect(await screen.findByRole("heading", { level: 2, name: "5 films in the last year" })).toBeInTheDocument();
    const summary = screen.getByText("Longest streak").closest("dl")!;
    expect(within(summary).getByText("Sunday")).toBeInTheDocument();
    expect(within(summary).getAllByText("2 days")).toHaveLength(2);

    const results = await axe.run(container, {
      runOnly: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
      rules: { "color-contrast": { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });

  it("moves between days with the arrow keys and opens a day with Enter", async () => {
    const user = userEvent.setup();
    renderStats({
      "GET /api/users/5/stats": () => json(stats),
      "GET /api/users/5/stats/days/2026-10-04": () =>
        json([
          { id: 1, tmdbId: 10, title: "Brazil", posterUrl: null, liked: null, rewatched: false, rating: 4, hasReview: false, watchedAt: "2026-10-04T12:00:00Z" },
          { id: 2, tmdbId: 11, title: "Alien", posterUrl: null, liked: null, rewatched: true, rating: null, hasReview: false, watchedAt: "2026-10-04T12:00:00Z" },
        ]),
    });
    await screen.findByRole("heading", { level: 2 });

    // Only the last day can be reached with Tab.
    await tabToGraph(user);
    expect(document.activeElement).toHaveAccessibleName(dayLabel("0 films", "October 12, 2026"));
    expect(screen.getAllByRole("button", { name: / on / }).filter((b) => b.tabIndex === 0)).toHaveLength(1);

    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toHaveAccessibleName(dayLabel("2 films", "October 11, 2026"));

    await user.keyboard("{ArrowLeft}");
    expect(document.activeElement).toHaveAccessibleName(dayLabel("2 films", "October 4, 2026"));

    await user.keyboard("{Enter}");
    expect(await screen.findByText("Brazil")).toBeInTheDocument();
    expect(screen.getByText("Rewatch")).toBeInTheDocument();
    expect(document.activeElement).toHaveAttribute("aria-pressed", "true");
  });

  it("stops at the first and last day", async () => {
    const user = userEvent.setup();
    renderStats({ "GET /api/users/5/stats": () => json(stats) });
    await screen.findByRole("heading", { level: 2 });

    await tabToGraph(user);
    await user.keyboard("{ArrowRight}");
    expect(document.activeElement).toHaveAccessibleName(dayLabel("0 films", "October 12, 2026"));

    await user.keyboard("{Home}");
    expect(document.activeElement).toHaveAccessibleName(dayLabel("0 films", "October 1, 2026"));
    await user.keyboard("{ArrowUp}");
    expect(document.activeElement).toHaveAccessibleName(dayLabel("0 films", "October 1, 2026"));
  });

  it("asks for another year when one is chosen", async () => {
    const user = userEvent.setup();
    const past: UserStats = { ...stats, from: "2025-01-01", to: "2025-12-31", total: 7, currentStreak: null };
    const { fetchMock } = renderStats({
      "GET /api/users/5/stats": (request) => json(new URL(request.url).searchParams.get("year") === "2025" ? past : stats),
    });
    await screen.findByRole("heading", { level: 2, name: "5 films in the last year" });

    await user.click(screen.getByRole("button", { name: "2025" }));

    expect(await screen.findByRole("heading", { level: 2, name: "7 films in 2025" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "2025" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.queryByText("Current streak")).not.toBeInTheDocument();
    expect(requestsOf(fetchMock).some((r) => r.url.endsWith("/api/users/5/stats?year=2025"))).toBe(true);
  });

  it("says so when nothing was logged", async () => {
    renderStats({
      "GET /api/users/5/stats": () => json({ ...stats, total: 0, days: [], busiestWeekday: null, years: [] }),
    });

    expect(await screen.findByText("No films logged in this period.")).toBeInTheDocument();
    expect(screen.queryAllByRole("button", { name: / on / })).toHaveLength(0);
  });

  it("shows nothing when the diary is hidden", async () => {
    const { container, fetchMock } = renderStats({});

    await screen.findByRole("main");
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(fetchMock).toHaveBeenCalled();
    expect(container.querySelector("section")).toBeNull();
  });
});
