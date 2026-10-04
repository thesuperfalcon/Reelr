import { Link, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { Poster } from "../components/Poster";
import { ReviewText } from "../components/ReviewText";
import { Stars } from "../components/Stars";
import { ErrorMessage, Loading } from "../components/Status";
import {
    useDiary,
    useFollowList,
    useUserProfile,
    useUserReviews,
    useWatchlist,
} from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { DiaryEntry, SearchMovie } from "../lib/types";

type Tab = "diary" | "reviews" | "watchlist" | "followers" | "following";

// Poster expects the TMDB search shape; watchlist rows carry only id, title and poster.
function asSearchMovie(entry: {
    tmdbId: number;
    title: string;
    posterUrl: string | null;
}): SearchMovie {
    return {
        id: entry.tmdbId,
        title: entry.title,
        poster_path: entry.posterUrl,
        overview: null,
        release_date: null,
        vote_average: 0,
    };
}

const monthFormat = new Intl.DateTimeFormat("en", {
    month: "long",
    year: "numeric",
});
const dayFormat = new Intl.DateTimeFormat("en", { day: "numeric" });
const weekdayFormat = new Intl.DateTimeFormat("en", { weekday: "short" });

function groupByMonth(
    entries: DiaryEntry[],
): { month: string; entries: DiaryEntry[] }[] {
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
            <Link
                to="/"
                className="mt-4 inline-block font-medium text-projector underline underline-offset-4"
            >
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
        return (
            <ErrorMessage error={diary.error} retry={() => diary.refetch()} />
        );
    }

    if (diary.data.length === 0) {
        return (
            <EmptyState>
                Your diary is empty. Rate a film to log it here.
            </EmptyState>
        );
    }

    return (
        <div className="mt-8 space-y-10">
            {groupByMonth(diary.data).map((group) => (
                <section key={group.month} aria-label={group.month}>
                    <h3 className="marquee text-2xl text-projector">
                        {group.month}
                    </h3>
                    <ol className="mt-3 divide-y divide-white/5">
                        {group.entries.map((entry) => {
                            const date = new Date(entry.watchedAt);
                            const poster = tmdbImage(entry.posterUrl, "w185");
                            return (
                                <li
                                    key={entry.id}
                                    className="flex items-center gap-4 py-3"
                                >
                                    <time
                                        dateTime={entry.watchedAt}
                                        className="w-10 shrink-0 text-center"
                                    >
                                        <span className="marquee block text-3xl">
                                            {dayFormat.format(date)}
                                        </span>
                                        <span className="block text-xs text-haze">
                                            {weekdayFormat.format(date)}
                                        </span>
                                    </time>

                                    <Link
                                        to={`/movie/${entry.tmdbId}`}
                                        className="shrink-0 rounded-sm"
                                        tabIndex={-1}
                                    >
                                        {poster ? (
                                            <img
                                                src={poster}
                                                alt=""
                                                loading="lazy"
                                                className="aspect-[2/3] w-10 rounded-sm object-cover"
                                            />
                                        ) : (
                                            <div
                                                className="aspect-[2/3] w-10 rounded-sm bg-row"
                                                aria-hidden="true"
                                            />
                                        )}
                                    </Link>

                                    <Link
                                        to={`/movie/${entry.tmdbId}`}
                                        className="min-w-0 flex-1 truncate font-medium hover:text-projector"
                                    >
                                        {entry.title}
                                    </Link>

                                    <div className="flex shrink-0 items-center gap-3 text-sm">
                                        {entry.rating !== null && (
                                            <Stars
                                                score={entry.rating}
                                                className="h-3.5 sm:h-4"
                                            />
                                        )}
                                        {entry.rewatched && (
                                            <span className="text-haze">
                                                Rewatch
                                            </span>
                                        )}
                                        {entry.hasReview && (
                                            <Link
                                                to="?tab=reviews"
                                                className="text-haze hover:text-screen"
                                                aria-label="Reviewed. Show reviews"
                                                title="Reviewed"
                                            >
                                                <svg
                                                    viewBox="0 0 24 24"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="2"
                                                    strokeLinecap="round"
                                                    className="size-4"
                                                    aria-hidden="true"
                                                >
                                                    <path d="M4 6h16M4 12h16M4 18h10" />
                                                </svg>
                                            </Link>
                                        )}
                                        {entry.liked === true && (
                                            <span
                                                className="text-alarm"
                                                aria-label="Liked"
                                                title="Liked"
                                            >
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

const reviewDateFormat = new Intl.DateTimeFormat("en", {
    day: "numeric",
    month: "short",
    year: "numeric",
});

function Reviews({ userId }: { userId: number }) {
    const reviews = useUserReviews(userId);

    if (reviews.isPending) {
        return <Loading label="Loading reviews" />;
    }

    if (reviews.isError) {
        return (
            <ErrorMessage
                error={reviews.error}
                retry={() => reviews.refetch()}
            />
        );
    }

    if (reviews.data.length === 0) {
        return (
            <EmptyState>
                You have not reviewed any films yet. Write one when you rate a
                film.
            </EmptyState>
        );
    }

    return (
        <ol className="mt-4 divide-y divide-white/5">
            {reviews.data.map((review) => {
                const poster = tmdbImage(review.posterUrl, "w185");
                return (
                    <li key={review.id} className="flex gap-4 py-6">
                        <Link
                            to={`/movie/${review.tmdbId}`}
                            className="shrink-0 self-start rounded-sm"
                            tabIndex={-1}
                        >
                            {poster ? (
                                <img
                                    src={poster}
                                    alt=""
                                    loading="lazy"
                                    className="aspect-[2/3] w-16 rounded-sm object-cover"
                                />
                            ) : (
                                <div
                                    className="aspect-[2/3] w-16 rounded-sm bg-row"
                                    aria-hidden="true"
                                />
                            )}
                        </Link>

                        <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                                <Link
                                    to={`/movie/${review.tmdbId}`}
                                    className="font-medium hover:text-projector"
                                >
                                    {review.title}
                                </Link>
                                {review.score !== null && (
                                    <Stars
                                        score={review.score}
                                        className="h-3.5"
                                    />
                                )}
                                <time
                                    dateTime={review.createdAt}
                                    className="text-sm text-haze"
                                >
                                    {reviewDateFormat.format(
                                        new Date(review.createdAt),
                                    )}
                                </time>
                                {review.updatedAt && (
                                    <span className="text-sm text-haze">
                                        (edited)
                                    </span>
                                )}
                            </div>
                            <ReviewText text={review.text} className="mt-2" />
                        </div>
                    </li>
                );
            })}
        </ol>
    );
}

function Watchlist() {
    const watchlist = useWatchlist();

    if (watchlist.isPending) {
        return <Loading label="Loading watchlist" />;
    }

    if (watchlist.isError) {
        return (
            <ErrorMessage
                error={watchlist.error}
                retry={() => watchlist.refetch()}
            />
        );
    }

    if (watchlist.data.length === 0) {
        return (
            <EmptyState>
                Your watchlist is empty. Add films you want to see.
            </EmptyState>
        );
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

function FollowList({
    userId,
    list,
}: {
    userId: number;
    list: "followers" | "following";
}) {
    const follows = useFollowList(userId, list);

    if (follows.isPending) {
        return (
            <Loading
                label={
                    list === "followers"
                        ? "Loading followers"
                        : "Loading following"
                }
            />
        );
    }

    if (follows.isError) {
        return (
            <ErrorMessage
                error={follows.error}
                retry={() => follows.refetch()}
            />
        );
    }

    if (follows.data.length === 0) {
        return (
            <p className="py-10 text-haze">
                {list === "followers"
                    ? "Nobody follows you yet."
                    : "You do not follow anyone yet."}
            </p>
        );
    }

    return (
        <ul className="mt-8 grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            {follows.data.map((person) => (
                <li key={person.id} className="flex items-center gap-3">
                    {person.profileImageUrl ? (
                        <img
                            src={person.profileImageUrl}
                            alt=""
                            loading="lazy"
                            className="size-12 shrink-0 rounded-full object-cover"
                        />
                    ) : (
                        <div
                            className="marquee flex size-12 shrink-0 items-center justify-center rounded-full bg-row text-xl text-haze"
                            aria-hidden="true"
                        >
                            {person.userName.charAt(0).toUpperCase()}
                        </div>
                    )}
                    <span className="truncate font-medium">
                        {person.userName}
                    </span>
                </li>
            ))}
        </ul>
    );
}

const tabIds: Tab[] = [
    "diary",
    "reviews",
    "watchlist",
    "followers",
    "following",
];

function isTab(value: string | null): value is Tab {
    return tabIds.includes(value as Tab);
}

export function ProfilePage() {
    // RequireAuth guarantees a user on this route.
    const user = useAuth().user!;
    const profile = useUserProfile(user.id);
    const [params, setParams] = useSearchParams();
    const requested = params.get("tab");
    const tab: Tab = isTab(requested) ? requested : "diary";

    const tabs: { id: Tab; label: string; count?: number }[] = [
        { id: "diary", label: "Diary" },
        { id: "reviews", label: "Reviews" },
        { id: "watchlist", label: "Watchlist" },
        {
            id: "followers",
            label: "Followers",
            count: profile.data?.followerCount,
        },
        {
            id: "following",
            label: "Following",
            count: profile.data?.followingCount,
        },
    ];

    return (
        <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
            <header>
                <p className="text-sm text-projector">Your profile</p>
                <h1 className="marquee mt-3 text-6xl sm:text-8xl">
                    {profile.data?.userName ?? user.username}
                </h1>
            </header>

            <div
                role="tablist"
                aria-label="Profile sections"
                className="mt-10 flex gap-6 overflow-x-auto overflow-y-hidden border-b border-white/5 scrollbar-none [&::-webkit-scrollbar]:hidden"
            >
                {tabs.map(({ id, label, count }) => (
                    <button
                        key={id}
                        type="button"
                        role="tab"
                        id={`tab-${id}`}
                        aria-selected={tab === id}
                        aria-controls={`panel-${id}`}
                        onClick={() =>
                            setParams(id === "diary" ? {} : { tab: id }, {
                                replace: true,
                            })
                        }
                        className={`-mb-px shrink-0 border-b-2 pb-3 text-sm font-medium transition-colors ${
                            tab === id
                                ? "border-projector text-screen"
                                : "border-transparent text-haze hover:text-screen"
                        }`}
                    >
                        {label}
                        {count !== undefined && (
                            <span className="ml-1.5 text-haze">{count}</span>
                        )}
                    </button>
                ))}
            </div>

            <div
                role="tabpanel"
                id={`panel-${tab}`}
                aria-labelledby={`tab-${tab}`}
            >
                {tab === "diary" && <Diary />}
                {tab === "reviews" && <Reviews userId={user.id} />}
                {tab === "watchlist" && <Watchlist />}
                {(tab === "followers" || tab === "following") && (
                    <FollowList userId={user.id} list={tab} />
                )}
            </div>
        </div>
    );
}
