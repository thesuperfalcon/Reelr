import { useEffect, useRef } from "react";

interface PagedQuery {
  hasNextPage: boolean;
  isFetchingNextPage: boolean;
  isFetchNextPageError: boolean;
  fetchNextPage: () => unknown;
}

interface LoadMoreProps {
  query: PagedQuery;
  /** What is being loaded, for the error message, e.g. "films". */
  what: string;
}

// Loads the next page when the button scrolls into view, so long lists continue as you scroll.
// The button stays for keyboard users and when the observer is not available. After a failed load
// it waits for a click, so a broken connection does not retry in a loop.
export function LoadMore({ query, what }: LoadMoreProps) {
  const buttonRef = useRef<HTMLButtonElement>(null);
  const { hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage } = query;

  useEffect(() => {
    const button = buttonRef.current;
    if (!button || !hasNextPage || isFetchingNextPage || isFetchNextPageError || typeof IntersectionObserver === "undefined") {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          fetchNextPage();
        }
      },
      { rootMargin: "400px 0px" },
    );
    observer.observe(button);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage]);

  if (!hasNextPage) {
    return null;
  }

  return (
    <div className="mt-6">
      {isFetchNextPageError && (
        <p role="alert" className="mb-2 text-sm text-alarm">
          Could not load more {what}. Try again.
        </p>
      )}
      <button
        ref={buttonRef}
        type="button"
        onClick={() => fetchNextPage()}
        disabled={isFetchingNextPage}
        className="rounded-sm px-4 py-2 text-sm font-medium text-screen ring-1 ring-rule/15 transition hover:bg-row disabled:opacity-50"
      >
        {isFetchingNextPage ? "Loading…" : "Load more"}
      </button>
    </div>
  );
}
