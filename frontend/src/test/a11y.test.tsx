import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { useState, type ReactNode } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { DeleteReviewButton } from "../components/DeleteReviewButton";
import { LoadMore } from "../components/LoadMore";
import { SpoilerGate } from "../components/SpoilerGate";
import { StarInput } from "../components/Stars";
import { moveBetweenTabs } from "../lib/tabs";
import { ForgotPasswordPage, LoginPage, RegisterPage } from "../pages/AuthPages";
import { mockFetch } from "./fakes";

// The automatic WCAG checks that make sense without a real browser. Colour contrast needs real layout and
// is checked in the browser instead.
async function expectNoAxeViolations(container: Element) {
  const results = await axe.run(container, {
    runOnly: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"],
    rules: { "color-contrast": { enabled: false } },
  });
  expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`)).toEqual([]);
}

function renderPage(page: ReactNode) {
  mockFetch({});
  const router = createMemoryRouter([{ path: "/", element: page }]);
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider>
        <main>
          <RouterProvider router={router} />
        </main>
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe("forms pass axe", () => {
  it.each([
    ["log in", <LoginPage />],
    ["create account", <RegisterPage />],
    ["forgot password", <ForgotPasswordPage />],
  ])("%s", async (_name, page) => {
    const { container } = renderPage(page);
    await screen.findByRole("heading", { level: 1 });

    await expectNoAxeViolations(container);
  });
});

describe("star input", () => {
  function Rating() {
    const [value, setValue] = useState<number | null>(null);
    return (
      <>
        <p id="rating-label">Rating</p>
        <StarInput value={value} onChange={setValue} labelledBy="rating-label" />
      </>
    );
  }

  it("is a labelled slider that arrow keys move in half stars", async () => {
    const { container } = render(<Rating />);
    const slider = screen.getByRole("slider", { name: "Rating" });
    const user = userEvent.setup();

    expect(slider).toHaveAttribute("aria-valuetext", "Not rated");
    slider.focus();
    await user.keyboard("{End}{ArrowLeft}");

    expect(slider).toHaveAttribute("aria-valuetext", "4.5 out of 5 stars");
    await expectNoAxeViolations(container);
  });
});

describe("focus is never dropped", () => {
  it("a delete confirmation focuses Keep, and Keep returns focus to the button", async () => {
    mockFetch({});
    render(
      <QueryClientProvider client={new QueryClient()}>
        <DeleteReviewButton reviewId={1} />
      </QueryClientProvider>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Delete review" }));
    expect(screen.getByRole("button", { name: "Keep" })).toHaveFocus();

    await user.keyboard("{Enter}");
    expect(screen.getByRole("button", { name: "Delete review" })).toHaveFocus();
  });

  it("showing a spoiler review moves focus to the text", async () => {
    render(
      <SpoilerGate active>
        <p>The twist is revealed.</p>
      </SpoilerGate>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Show review" }));

    expect(screen.getByText("The twist is revealed.").parentElement).toHaveFocus();
  });
});

describe("tabs", () => {
  function Tabs() {
    const [selected, setSelected] = useState("Diary");
    return (
      <div role="tablist" aria-label="Sections" onKeyDown={moveBetweenTabs}>
        {["Diary", "Reviews", "Lists"].map((name) => (
          <button
            key={name}
            type="button"
            role="tab"
            aria-selected={selected === name}
            tabIndex={selected === name ? 0 : -1}
            onClick={() => setSelected(name)}
          >
            {name}
          </button>
        ))}
      </div>
    );
  }

  it("arrow keys, Home and End move between tabs and select them", async () => {
    render(<Tabs />);
    const user = userEvent.setup();

    await user.tab();
    expect(screen.getByRole("tab", { name: "Diary" })).toHaveFocus();

    await user.keyboard("{ArrowRight}");
    expect(screen.getByRole("tab", { name: "Reviews" })).toHaveFocus();
    expect(screen.getByRole("tab", { name: "Reviews" })).toHaveAttribute("aria-selected", "true");

    await user.keyboard("{ArrowLeft}{ArrowLeft}");
    expect(screen.getByRole("tab", { name: "Lists" })).toHaveFocus();

    await user.keyboard("{Home}");
    expect(screen.getByRole("tab", { name: "Diary" })).toHaveFocus();
  });
});

describe("load more", () => {
  it("is a named button while there are more pages, and says when loading failed", async () => {
    const { container, rerender } = render(
      <LoadMore query={{ hasNextPage: true, isFetchingNextPage: false, isFetchNextPageError: false, fetchNextPage: () => undefined }} what="films" />,
    );
    expect(screen.getByRole("button", { name: "Load more" })).toBeInTheDocument();
    await expectNoAxeViolations(container);

    rerender(
      <LoadMore query={{ hasNextPage: true, isFetchingNextPage: false, isFetchNextPageError: true, fetchNextPage: () => undefined }} what="films" />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Could not load more films");
  });
});
