import { Link, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { ActivityFeed } from "../components/ActivityFeed";
import { type FeedKind, useUserProfile } from "../lib/queries";

const feedTabs: { id: FeedKind; label: string }[] = [
  { id: "following", label: "Following" },
  { id: "community", label: "Community" },
];

const communityEmpty = "Nothing logged on Reelr in the last 30 days yet.";

// The full activity stream. Following is the default once you follow someone; until then Community,
// so the page is never empty. Logged out, only Community exists.
export function ActivityPage() {
  const { user } = useAuth();
  const profile = useUserProfile(user?.id ?? 0, user !== null);
  const [params, setParams] = useSearchParams();

  const requested = params.get("feed");
  const fallback: FeedKind = profile.data?.followingCount === 0 ? "community" : "following";
  const tab: FeedKind = requested === "following" || requested === "community" ? requested : fallback;

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      <section aria-labelledby="feed-title" className="max-w-3xl">
        <h1 id="feed-title" className="marquee text-5xl sm:text-6xl">
          Activity
        </h1>

        {!user ? (
          <>
            <p className="mt-3 text-haze">What people on Reelr have watched, reviewed and listed lately.</p>
            <ActivityFeed kind="community" empty={communityEmpty} />
          </>
        ) : (
          <>
            <div role="tablist" aria-label="Activity feeds" className="mt-6 flex gap-6 border-b border-white/5">
              {feedTabs.map(({ id, label }) => (
                <button
                  key={id}
                  type="button"
                  role="tab"
                  id={`feed-tab-${id}`}
                  aria-selected={tab === id}
                  aria-controls="feed-panel"
                  onClick={() => setParams({ feed: id }, { replace: true, preventScrollReset: true })}
                  className={`-mb-px shrink-0 border-b-2 pb-3 text-sm font-medium transition-colors ${
                    tab === id ? "border-projector text-screen" : "border-transparent text-haze hover:text-screen"
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
            <div role="tabpanel" id="feed-panel" aria-labelledby={`feed-tab-${tab}`}>
              {tab === "following" ? (
                <ActivityFeed
                  key="following"
                  kind="following"
                  empty={
                    <>
                      <p>Follow people to see what they watch, review and list.</p>
                      <Link to="/search" className="mt-3 inline-block font-medium text-projector underline underline-offset-4">
                        Find people to follow
                      </Link>
                    </>
                  }
                />
              ) : (
                <ActivityFeed key="community" kind="community" empty={communityEmpty} />
              )}
            </div>
          </>
        )}
      </section>
    </div>
  );
}
