import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router";
import { describe, expect, it } from "vitest";
import { AuthProvider } from "../auth/AuthProvider";
import { JoinBar } from "../components/JoinBar";
import { mockFetch } from "./fakes";

function renderAt(path: string) {
  mockFetch({});
  const router = createMemoryRouter([{ path: "*", element: <JoinBar /> }], { initialEntries: [path] });
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

describe("sign-up bar on phones", () => {
  it("offers visitors who are not logged in a way to join", async () => {
    renderAt("/movie/550");

    const bar = await screen.findByRole("complementary", { name: "Join Reelr" });
    expect(bar).toHaveClass("sm:hidden");
    expect(screen.getByRole("link", { name: "Create account" })).toHaveAttribute("href", "/register");
  });

  it("stays away from the pages for logging in and joining", () => {
    renderAt("/register");

    expect(screen.queryByRole("complementary", { name: "Join Reelr" })).not.toBeInTheDocument();
  });
});
