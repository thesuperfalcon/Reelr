import type { ReactNode } from "react";
import { isKnownActivity } from "../lib/activity";
import { useActivityFeed, type FeedKind } from "../lib/queries";
import { ActivityItem } from "./ActivityItem";
import { LoadMore } from "./LoadMore";
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

      <LoadMore query={feed} what="activity" />
    </div>
  );
}
