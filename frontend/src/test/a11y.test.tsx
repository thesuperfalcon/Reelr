import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { useState, type ReactNode } from "react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { DeleteReviewButton } from "../components/DeleteReviewButton";
import { Dialog } from "../components/Dialog";
import { LoadMore } from "../components/LoadMore";
import { SpoilerGate } from "../components/SpoilerGate";
import { StarInput } from "../components/Stars";
import { PosterGridSkeleton } from "../components/Status";
import { Tabs } from "../components/Tabs";
import { moveBetweenTabs } from "../lib/tabs";
import { ForgotPasswordPage, LoginPage, RegisterPage } from "../pages/AuthPages";
import { mockFetch } from "./fakes";

// The automatic WCAG checks that make sense without a real browser. Colour contrast needs real layout and
// is checked in the browser instead, in e2e/tests/accessibility.spec.ts.
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

describe("shared tabs", () => {
  function Sections() {
    const [selected, setSelected] = useState<"diary" | "reviews" | "lists">("diary");
    return (
      <>
        <Tabs
          label="Sections"
          tabs={[
            { id: "diary", label: "Diary" },
            { id: "reviews", label: "Reviews", count: 3 },
            { id: "lists", label: "Lists" },
          ]}
          selected={selected}
          onSelect={setSelected}
          tabId={(id) => `tab-${id}`}
          panelId="panel"
        />
        <div role="tabpanel" id="panel" aria-labelledby={`tab-${selected}`}>
          {selected}
        </div>
      </>
    );
  }

  it("keeps only the selected tab in the Tab order and labels the panel with it", async () => {
    const { container } = render(<Sections />);
    const user = userEvent.setup();

    await user.tab();
    expect(screen.getByRole("tab", { name: "Diary" })).toHaveFocus();
    expect(screen.getByRole("tab", { name: "Reviews 3" })).toHaveAttribute("tabindex", "-1");

    await user.keyboard("{End}");
    expect(screen.getByRole("tab", { name: "Lists" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tabpanel", { name: "Lists" })).toHaveTextContent("lists");
    await expectNoAxeViolations(container);
  });
});

describe("dialog", () => {
  it("puts focus back on the button that opened it when it closes", async () => {
    // jsdom has no showModal; the browser behaviour under test is ours, not the dialog's.
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.open = true;
    });

    function Opener() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button type="button" onClick={() => setOpen(true)}>
            Open
          </button>
          {open && (
            <Dialog labelledBy="dialog-title" onClose={() => setOpen(false)}>
              <h2 id="dialog-title">Title</h2>
              <button type="button" onClick={() => setOpen(false)}>
                Close
              </button>
            </Dialog>
          )}
        </>
      );
    }

    render(<Opener />);
    const user = userEvent.setup();

    await user.click(screen.getByRole("button", { name: "Open" }));
    await user.click(screen.getByRole("button", { name: "Close" }));

    expect(screen.getByRole("button", { name: "Open" })).toHaveFocus();
  });
});

describe("loading placeholders", () => {
  it("announce what is loading while the shapes stay hidden", async () => {
    const { container } = render(<PosterGridSkeleton label="Loading films" />);

    expect(screen.getByRole("status")).toHaveTextContent("Loading films…");
    expect(container.querySelector("ul")).toHaveAttribute("aria-hidden", "true");
    await expectNoAxeViolations(container);
  });
});
