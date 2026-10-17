import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import { useDeleteDiaryEntry } from "../lib/queries";
import { useUserStats } from "../lib/stats";
import { json, mockFetch, requestsOf } from "./fakes";

describe("diary changes", () => {
  it("fetch the stats on screen again after an entry is deleted", async () => {
    const fetchMock = mockFetch({
      "GET /api/users/5/stats": () => json({ total: 1 }),
      "DELETE /api/diary/1": () => new Response(null, { status: 204 }),
    });
    // The app's staleTime, so only invalidation can explain a second fetch.
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 5 * 60 * 1000 } } });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const statsRequests = () => requestsOf(fetchMock).filter((r) => r.url.endsWith("/api/users/5/stats")).length;

    const { result } = renderHook(() => ({ stats: useUserStats(5, null), remove: useDeleteDiaryEntry() }), { wrapper });
    await waitFor(() => expect(result.current.stats.isSuccess).toBe(true));
    expect(statsRequests()).toBe(1);

    await act(() => result.current.remove.mutateAsync(1));

    await waitFor(() => expect(statsRequests()).toBe(2));
  });
});
