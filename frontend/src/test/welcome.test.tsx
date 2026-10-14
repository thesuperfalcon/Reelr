import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import axe from "axe-core";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { Welcome } from "../components/Welcome";
import { mockFetch } from "./fakes";

describe("welcome on the start page", () => {
  it("tells visitors who are not logged in what Reelr is and how to join", async () => {
    mockFetch({});
    const router = createMemoryRouter([{ path: "/", element: <main><Welcome /></main> }]);
    const { container } = render(
      <QueryClientProvider client={new QueryClient()}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>,
    );

    expect(await screen.findByRole("region", { name: "About Reelr" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "I have an invite" })).toHaveAttribute("href", "/register");
    expect(screen.getByRole("link", { name: "Log in" })).toHaveAttribute("href", "/login");

    const results = await axe.run(container, { runOnly: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"], rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });
});
