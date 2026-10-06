import type { ReactNode } from "react";
import { isKnownActivity } from "../lib/activity";
import { useActivityFeed, type FeedKind } from "../lib/queries";
import { ActivityItem } from "./ActivityItem";
import { ErrorMessage, Loading } from "./Status";

interface ActivityFeedProps {
  kind: FeedKind;
  /** Shown when the feed has nothing at all. */
  empty: ReactNode;
}

export function ActivityFeed({ kind, empty }: ActivityFeedProps) {
  const feed = useActivityFeed(kind);

  if (feed.isPending) {
    return <Loading label="Loading activity" />;
  }

  if (feed.isError) {
    return <ErrorMessage error={feed.error} retry={() => feed.refetch()} />;
  }

  const items = feed.data.pages.flatMap((page) => page.items).filter(isKnownActivity);

  if (items.length === 0 && !feed.hasNextPage) {
    return <div className="py-8 text-haze">{empty}</div>;
  }

  return (
    <div>
      <ul className="divide-y divide-white/5">
        {items.map((item) => (
          <ActivityItem key={item.id} item={item} />
        ))}
      </ul>

      {feed.isFetchNextPageError && (
        <p role="alert" className="mt-2 text-sm text-alarm">
          Could not load more activity. Try again.
        </p>
      )}

      {feed.hasNextPage && (
        <button
          type="button"
          onClick={() => feed.fetchNextPage()}
          disabled={feed.isFetchingNextPage}
          className="mt-4 rounded-sm px-4 py-2 text-sm font-medium text-screen ring-1 ring-white/15 transition hover:bg-row disabled:opacity-50"
        >
          {feed.isFetchingNextPage ? "Loading…" : "Load more"}
        </button>
      )}
    </div>
  );
}
