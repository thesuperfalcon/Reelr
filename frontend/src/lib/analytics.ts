import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { useLocation, useMatches } from "react-router";
import { api } from "./api";

// Page views are counted per route and day on our own server, without cookies or anything that identifies the
// visitor (backend Features/Analytics). The privacy page describes this; keep it in step.

export interface PageViewStats {
  days: number;
  totalViews: number;
  perDay: { day: string; views: number }[];
  perRoute: { route: string; views: number }[];
}

// The route a page belongs to, with its ids as names: /user/42 becomes /user/:userId, so no address with an id
// in it is ever sent. Any unknown address is "/*", the "not found" page.
export function routeOf(pathname: string, params: Record<string, string | undefined>): string {
  if (params["*"] !== undefined) {
    return "/*";
  }

  const byValue = new Map(Object.entries(params).map(([name, value]) => [value, name]));
  const route = pathname
    .split("/")
    .map((segment) => {
      const name = byValue.get(decodeURIComponent(segment));
      return segment && name ? `:${name}` : segment;
    })
    .join("/");

  return route.length > 1 ? route.replace(/\/+$/, "") : route;
}

function visitorAskedNotToBeTracked(): boolean {
  const browser = navigator as Navigator & { globalPrivacyControl?: boolean };
  return browser.doNotTrack === "1" || browser.globalPrivacyControl === true;
}

// Counts each page the visitor opens. Failures are ignored: a missed count must never show as an error.
export function usePageViews() {
  const { pathname } = useLocation();
  const matches = useMatches();
  const route = routeOf(pathname, matches.at(-1)?.params ?? {});

  useEffect(() => {
    if (visitorAskedNotToBeTracked()) {
      return;
    }
    api<void>("/api/analytics/page-views", { method: "POST", body: JSON.stringify({ route }) }).catch(() => {});
    // The pathname too, so moving from one film to another counts both.
  }, [pathname, route]);
}

export function usePageViewStats(days = 30) {
  return useQuery({
    queryKey: ["admin", "page-views", days],
    queryFn: () => api<PageViewStats>(`/api/admin/page-views?days=${days}`),
  });
}
