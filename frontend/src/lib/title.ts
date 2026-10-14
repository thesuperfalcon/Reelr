import { useEffect } from "react";

const DESCRIPTION_MAX = 160;

// Search results show about this much, so longer texts end at a word with an ellipsis.
export function shortDescription(text: string): string {
  const flat = text.replace(/\s+/g, " ").trim();
  if (flat.length <= DESCRIPTION_MAX) {
    return flat;
  }
  const cut = flat.slice(0, DESCRIPTION_MAX - 1);
  const lastSpace = cut.lastIndexOf(" ");
  return `${(lastSpace > 0 ? cut.slice(0, lastSpace) : cut).replace(/[\s.,;:]+$/, "")}…`;
}

// Names the browser tab after the page, so screen reader users hear where they landed and tabs and
// history entries can be told apart (WCAG 2.4.2). An empty title gives plain "Reelr". Undefined, as
// while a page's data loads, leaves the title alone, so a "not found" page shown inside it keeps its own.
// The description is what search engines and the browser show for the page; without one the site's own is used.
export function usePageTitle(title: string | undefined, description?: string | null) {
  useEffect(() => {
    if (title !== undefined) {
      document.title = title ? `${title} · Reelr` : "Reelr";
    }
  }, [title]);

  useEffect(() => {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="description"]');
    if (title !== undefined && meta) {
      // The site-wide description from index.html is kept aside the first time, for pages without their own.
      meta.dataset.default ??= meta.content;
      meta.content = description ? shortDescription(description) : meta.dataset.default;
    }
  }, [title, description]);
}
