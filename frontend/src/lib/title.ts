import { useEffect } from "react";

// Names the browser tab after the page, so screen reader users hear where they landed and tabs and
// history entries can be told apart (WCAG 2.4.2). An empty title gives plain "Reelr". Undefined, as
// while a page's data loads, leaves the title alone, so a "not found" page shown inside it keeps its own.
export function usePageTitle(title: string | undefined) {
  useEffect(() => {
    if (title !== undefined) {
      document.title = title ? `${title} · Reelr` : "Reelr";
    }
  }, [title]);
}
