import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import axe from "axe-core";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { CookieNotice } from "../components/CookieNotice";

function renderNotice() {
  const router = createMemoryRouter([
    {
      path: "/",
      element: (
        <>
          <CookieNotice />
          <main id="main" tabIndex={-1} />
        </>
      ),
    },
  ]);
  return render(<RouterProvider router={router} />);
}

describe("cookie notice", () => {
  it("says what is kept, passes axe, and puts focus on the page when closed", async () => {
    const { container } = renderNotice();
    const notice = screen.getByRole("region", { name: "Cookies" });
    expect(notice).toHaveTextContent("one cookie, only to keep you logged in");

    const results = await axe.run(container, { runOnly: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"], rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);

    await userEvent.setup().click(screen.getByRole("button", { name: "OK" }));

    expect(screen.queryByRole("region", { name: "Cookies" })).not.toBeInTheDocument();
    expect(document.activeElement).toBe(screen.getByRole("main"));
  });

  it("stays closed once it has been seen", () => {
    localStorage.setItem("reelr.cookieNotice", "1");

    renderNotice();

    expect(screen.queryByRole("region", { name: "Cookies" })).not.toBeInTheDocument();
  });
});
