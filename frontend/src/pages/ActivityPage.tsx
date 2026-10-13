import { Link, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { ActivityFeed } from "../components/ActivityFeed";
import { Tabs } from "../components/Tabs";
import { type FeedKind, useSettings, useUpdateSettings, useUserProfile } from "../lib/queries";
import { usePageTitle } from "../lib/title";

const feedTabs: { id: FeedKind; label: string }[] = [
  { id: "following", label: "Following" },
  { id: "community", label: "Community" },
];

const communityEmpty = "Nothing logged on Reelr in the last 30 days yet.";

// Shows or hides the user's own activity in Following. The choice is the showOwnActivity setting.
function OwnActivityToggle() {
  const settings = useSettings();
  const update = useUpdateSettings();

  if (!settings.data) {
    return null;
  }

  return (
    <div className="mt-4 flex flex-wrap items-center justify-end gap-x-4 gap-y-1 text-sm">
      {update.isError && (
        <span role="alert" className="text-alarm">
          Could not save that. Try again.
        </span>
      )}
      <label className="flex min-h-10 cursor-pointer items-center gap-2 text-haze hover:text-screen">
        <input
          type="checkbox"
          checked={settings.data.showOwnActivity}
          onChange={(event) => update.mutate({ showOwnActivity: event.target.checked })}
          disabled={update.isPending}
          className="check"
        />
        Show my activity
      </label>
    </div>
  );
}

// The full activity stream. Following is the default once you follow someone; until then Community,
// so the page is never empty. Logged out, only Community exists.
export function ActivityPage() {
  usePageTitle("Activity");
  const { user } = useAuth();
  const profile = useUserProfile(user?.id ?? 0, user !== null);
  const [params, setParams] = useSearchParams();

  const requested = params.get("feed");
  const fallback: FeedKind = profile.data?.followingCount === 0 ? "community" : "following";
  const tab: FeedKind = requested === "following" || requested === "community" ? requested : fallback;

  return (
    <div className="page py-10 sm:py-14">
      <section aria-labelledby="feed-title" className="max-w-3xl">
        <h1 id="feed-title" className="heading-page">
          Activity
        </h1>

        {!user ? (
          <>
            <p className="mt-4 text-haze">What people on Reelr have watched, reviewed and listed lately.</p>
            <ActivityFeed kind="community" empty={communityEmpty} />
          </>
        ) : (
          <>
            <Tabs
              label="Activity feeds"
              tabs={feedTabs}
              selected={tab}
              onSelect={(id) => setParams({ feed: id }, { replace: true, preventScrollReset: true })}
              tabId={(id) => `feed-tab-${id}`}
              panelId="feed-panel"
              className="mt-8"
            />
            <div role="tabpanel" id="feed-panel" aria-labelledby={`feed-tab-${tab}`}>
              {tab === "following" && <OwnActivityToggle />}
              {tab === "following" ? (
                <ActivityFeed
                  key="following"
                  kind="following"
                  empty={
                    <>
                      <p>Follow people to see what they watch, review and list.</p>
                      <Link to="/search" className="btn btn-primary mt-5">
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
