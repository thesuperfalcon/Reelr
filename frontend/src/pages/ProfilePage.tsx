import { Link, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { useDiary, useUserProfile, useWatchlist } from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { DiaryEntry, SearchMovie } from "../lib/types";

type Tab = "diary" | "watchlist";

// Poster expects the TMDB search shape; watchlist rows carry only id, title and poster.
function asSearchMovie(entry: { tmdbId: number; title: string; posterUrl: string | null }): SearchMovie {
  return {
    id: entry.tmdbId,
    title: entry.title,
    poster_path: entry.posterUrl,
    overview: null,
    release_date: null,
    vote_average: 0,
  };
}

const monthFormat = new Intl.DateTimeFormat("en", { month: "long", year: "numeric" });
const dayFormat = new Intl.DateTimeFormat("en", { day: "numeric" });
const weekdayFormat = new Intl.DateTimeFormat("en", { weekday: "short" });

function groupByMonth(entries: DiaryEntry[]): { month: string; entries: DiaryEntry[] }[] {
  const groups: { month: string; entries: DiaryEntry[] }[] = [];

  for (const entry of entries) {
    const month = monthFormat.format(new Date(entry.watchedAt));
    const last = groups.at(-1);
    if (last?.month === month) {
      last.entries.push(entry);
    } else {
      groups.push({ month, entries: [entry] });
    }
  }

  return groups;
}

function EmptyState({ children }: { children: React.ReactNode }) {
  return (
    <div className="py-10">
      <p className="text-haze">{children}</p>
      <Link to="/" className="mt-4 inline-block font-medium text-projector underline underline-offset-4">
        Find films
      </Link>
    </div>
  );
}

function Diary() {
  const diary = useDiary();

  if (diary.isPending) {
    return <Loading label="Loading diary" />;
  }

  if (diary.isError) {
    return <ErrorMessage error={diary.error} retry={() => diary.refetch()} />;
  }

  if (diary.data.length === 0) {
    return <EmptyState>Your diary is empty. Mark a film as watched to log it here.</EmptyState>;
  }

  return (
    <div className="mt-8 space-y-10">
      {groupByMonth(diary.data).map((group) => (
        <section key={group.month} aria-label={group.month}>
          <h3 className="marquee text-2xl text-projector">{group.month}</h3>
          <ol className="mt-3 divide-y divide-white/5">
            {group.entries.map((entry) => {
              const date = new Date(entry.watchedAt);
              const poster = tmdbImage(entry.posterUrl, "w185");
              return (
                <li key={entry.tmdbId} className="flex items-center gap-4 py-3">
                  <time dateTime={entry.watchedAt} className="w-10 shrink-0 text-center">
                    <span className="marquee block text-3xl">{dayFormat.format(date)}</span>
                    <span className="block text-xs text-haze">{weekdayFormat.format(date)}</span>
                  </time>

                  <Link to={`/movie/${entry.tmdbId}`} className="shrink-0 rounded-sm" tabIndex={-1}>
                    {poster ? (
                      <img src={poster} alt="" loading="lazy" className="aspect-[2/3] w-10 rounded-sm object-cover" />
                    ) : (
                      <div className="aspect-[2/3] w-10 rounded-sm bg-row" aria-hidden="true" />
                    )}
                  </Link>

                  <Link
                    to={`/movie/${entry.tmdbId}`}
                    className="min-w-0 flex-1 truncate font-medium hover:text-projector"
                  >
                    {entry.title}
                  </Link>

                  <div className="flex shrink-0 gap-3 text-sm">
                    {entry.rewatched && <span className="text-haze">Rewatch</span>}
                    {entry.liked === true && (
                      <span className="text-alarm" aria-label="Liked" title="Liked">
                        ♥
                      </span>
                    )}
                  </div>
                </li>
              );
            })}
          </ol>
        </section>
      ))}
    </div>
  );
}

function Watchlist() {
  const watchlist = useWatchlist();

  if (watchlist.isPending) {
    return <Loading label="Loading watchlist" />;
  }

  if (watchlist.isError) {
    return <ErrorMessage error={watchlist.error} retry={() => watchlist.refetch()} />;
  }

  if (watchlist.data.length === 0) {
    return <EmptyState>Your watchlist is empty. Add films you want to see.</EmptyState>;
  }

  return (
    <ul className="mt-8 grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6 lg:gap-4">
      {watchlist.data.map((entry) => (
        <li key={entry.tmdbId}>
          <Poster movie={asSearchMovie(entry)} size="w342" />
          <p className="mt-2 truncate text-sm">{entry.title}</p>
        </li>
      ))}
    </ul>
  );
}

const tabs: { id: Tab; label: string }[] = [
  { id: "diary", label: "Diary" },
  { id: "watchlist", label: "Watchlist" },
];

export function ProfilePage() {
  // RequireAuth guarantees a user on this route.
  const user = useAuth().user!;
  const profile = useUserProfile(user.id);
  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get("tab") === "watchlist" ? "watchlist" : "diary";

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      <header>
        <p className="text-sm text-projector">Your profile</p>
        <h1 className="marquee mt-3 text-6xl sm:text-8xl">{profile.data?.userName ?? user.username}</h1>
        {profile.data && (
          <p className="mt-4 flex gap-6 text-sm text-haze">
            <span>
              <span className="font-semibold text-screen">{profile.data.followerCount}</span> followers
            </span>
            <span>
              <span className="font-semibold text-screen">{profile.data.followingCount}</span> following
            </span>
          </p>
        )}
      </header>

      <div role="tablist" aria-label="Profile sections" className="mt-10 flex gap-6 border-b border-white/5">
        {tabs.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            role="tab"
            id={`tab-${id}`}
            aria-selected={tab === id}
            aria-controls={`panel-${id}`}
            onClick={() => setParams(id === "diary" ? {} : { tab: id }, { replace: true })}
            className={`-mb-px border-b-2 pb-3 text-sm font-medium transition-colors ${
              tab === id ? "border-projector text-screen" : "border-transparent text-haze hover:text-screen"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
        {tab === "diary" ? <Diary /> : <Watchlist />}
      </div>
    </div>
  );
}
