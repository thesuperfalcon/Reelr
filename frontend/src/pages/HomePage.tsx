import { Link, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { ActivityFeed } from "../components/ActivityFeed";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { type FeedKind, usePopularMovies, useTrendingMovies, useUserProfile } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { SearchMovie } from "../lib/types";

function Feature({ movie }: { movie: SearchMovie }) {
  const poster = tmdbImage(movie.poster_path, "w500");
  const year = releaseYear(movie.release_date);

  return (
    <section aria-labelledby="feature-title" className="grid items-end gap-8 md:grid-cols-[minmax(0,20rem)_1fr] md:gap-12">
      <Link to={`/movie/${movie.id}`} className="block w-40 rounded-sm sm:w-56 md:w-full" tabIndex={-1}>
        {poster && (
          <img src={poster} alt="" className="aspect-[2/3] w-full rounded-sm object-cover shadow-[0_30px_80px_-20px_rgba(242,181,68,0.35)]" />
        )}
      </Link>

      <div className="max-w-2xl pb-2">
        <p className="text-sm text-projector">Most watched this week</p>
        <h1 id="feature-title" className="marquee mt-3 text-6xl sm:text-8xl lg:text-9xl">
          <Link to={`/movie/${movie.id}`} className="rounded-sm hover:text-projector">
            {movie.title}
          </Link>
        </h1>
        {year && <p className="mt-4 text-haze">{year}</p>}
        {movie.overview && <p className="mt-4 max-w-prose text-screen/85">{movie.overview}</p>}
      </div>
    </section>
  );
}

const feedTabs: { id: FeedKind; label: string }[] = [
  { id: "following", label: "Following" },
  { id: "community", label: "Community" },
];

// Following is the default once you follow someone; until then Community, so the page is never empty.
function FeedSection() {
  const { user } = useAuth();
  const profile = useUserProfile(user?.id ?? 0, user !== null);
  const [params, setParams] = useSearchParams();

  if (!user) {
    return (
      <section aria-labelledby="feed-title" className="mt-16 max-w-3xl">
        <h2 id="feed-title" className="marquee text-3xl">
          Recent on Reelr
        </h2>
        <ActivityFeed kind="community" empty="Nothing logged on Reelr in the last 30 days yet." />
      </section>
    );
  }

  const requested = params.get("feed");
  const fallback: FeedKind = profile.data?.followingCount === 0 ? "community" : "following";
  const tab: FeedKind = requested === "following" || requested === "community" ? requested : fallback;

  return (
    <section aria-labelledby="feed-title" className="mt-16 max-w-3xl">
      <h2 id="feed-title" className="marquee text-3xl">
        Activity
      </h2>
      <div role="tablist" aria-label="Activity feeds" className="mt-4 flex gap-6 border-b border-white/5">
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
          <ActivityFeed key="community" kind="community" empty="Nothing logged on Reelr in the last 30 days yet." />
        )}
      </div>
    </section>
  );
}

function MovieShelf({ title, movies }: { title: string; movies: SearchMovie[] }) {
  return (
    <section aria-label={title} className="mt-16">
      <h2 className="marquee text-3xl text-screen">{title}</h2>
      <ul className="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6 lg:gap-4">
        {movies.map((movie) => (
          <li key={movie.id}>
            <Poster movie={movie} size="w342" />
          </li>
        ))}
      </ul>
    </section>
  );
}

export function HomePage() {
  const trending = useTrendingMovies();
  const popular = usePopularMovies();

  if (trending.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading this week's films" />
      </div>
    );
  }

  if (trending.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={trending.error} retry={() => trending.refetch()} />
      </div>
    );
  }

  const [featured, ...rest] = trending.data.results;
  const popularMovies = popular.data?.results.filter((m) => m.id !== featured?.id) ?? [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      {featured && <Feature movie={featured} />}

      <FeedSection />

      <MovieShelf title="Trending this week" movies={rest.slice(0, 12)} />

      {popular.isError ? (
        <ErrorMessage error={popular.error} retry={() => popular.refetch()} />
      ) : (
        popularMovies.length > 0 && <MovieShelf title="Popular right now" movies={popularMovies.slice(0, 12)} />
      )}
    </div>
  );
}
