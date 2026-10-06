import type { UseQueryResult } from "@tanstack/react-query";
import { useState } from "react";
import {
    Link,
    Navigate,
    useNavigate,
    useParams,
    useSearchParams,
} from "react-router";
import { useAuth } from "../auth/auth-context";
import { ListCard } from "../components/ListCard";
import { ListFormDialog } from "../components/ListFormDialog";
import { Poster } from "../components/Poster";
import { ReviewText } from "../components/ReviewText";
import { Stars } from "../components/Stars";
import { ErrorMessage, Loading } from "../components/Status";
import { UserAvatar, UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import {
    useDiary,
    useFollowList,
    useToggleFollow,
    useUserDiary,
    useUserLists,
    useUserProfile,
    useUserReviews,
    useWatchlist,
} from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { DiaryEntry, SearchMovie, UserProfile } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";

type Tab =
    | "diary"
    | "reviews"
    | "lists"
    | "watchlist"
    | "followers"
    | "following";

// Who the page belongs to. Empty states and private tabs depend on it.
interface Owner {
    id: number;
    name: string;
    own: boolean;
}

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

function EmptyState({
    owner,
    children,
}: {
    owner: Owner;
    children: React.ReactNode;
}) {
    return (
        <div className="py-10">
            <p className="text-haze">{children}</p>
            {owner.own && (
                <Link
                    to="/"
                    className="mt-4 inline-block font-medium text-projector underline underline-offset-4"
                >
                    Find films
                </Link>
            )}
        </div>
    );
}

// The own diary uses the "me" query, which the rating dialog refreshes after each log.
function OwnDiary({ owner }: { owner: Owner }) {
    return <Diary owner={owner} diary={useDiary()} />;
}

function UserDiary({ owner }: { owner: Owner }) {
    return <Diary owner={owner} diary={useUserDiary(owner.id)} />;
}

function Diary({
    owner,
    diary,
}: {
    owner: Owner;
    diary: UseQueryResult<DiaryEntry[]>;
}) {
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
            <EmptyState owner={owner}>
                {owner.own
                    ? "Your diary is empty. Rate a film to log it here."
                    : `${owner.name} has not logged any films yet.`}
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

function Reviews({ owner }: { owner: Owner }) {
    const reviews = useUserReviews(owner.id);

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
            <EmptyState owner={owner}>
                {owner.own
                    ? "You have not reviewed any films yet. Write one when you rate a film."
                    : `${owner.name} has not reviewed any films yet.`}
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

function Lists({ owner }: { owner: Owner }) {
    const lists = useUserLists(owner.id);
    const navigate = useNavigate();
    const [creating, setCreating] = useState(false);

    let content: React.ReactNode;
    if (lists.isPending) {
        content = <Loading label="Loading lists" />;
    } else if (lists.isError) {
        content = (
            <ErrorMessage error={lists.error} retry={() => lists.refetch()} />
        );
    } else if (lists.data.length === 0) {
        content = (
            <p className="py-10 text-haze">
                {owner.own
                    ? "You have no lists yet. Make one here or from a film's page."
                    : `${owner.name} has no public lists yet.`}
            </p>
        );
    } else {
        content = (
            <ul className="mt-8 grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-4">
                {lists.data.map((list) => (
                    <li key={list.id}>
                        <ListCard list={list} />
                    </li>
                ))}
            </ul>
        );
    }

    return (
        <div>
            {owner.own && (
                <div className="mt-8 flex justify-end">
                    <button
                        type="button"
                        onClick={() => setCreating(true)}
                        aria-haspopup="dialog"
                        className="inline-flex h-9 items-center rounded-sm bg-projector px-4 text-sm font-semibold text-salon transition hover:brightness-110"
                    >
                        New list
                    </button>
                </div>
            )}
            {content}
            {creating && (
                <ListFormDialog
                    current={null}
                    onSaved={(list) => navigate(`/list/${list.id}`)}
                    onClose={() => setCreating(false)}
                />
            )}
        </div>
    );
}

// The watchlist API only serves the logged-in user's own list.
function Watchlist({ owner }: { owner: Owner }) {
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
            <EmptyState owner={owner}>
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
    owner,
    list,
}: {
    owner: Owner;
    list: "followers" | "following";
}) {
    const follows = useFollowList(owner.id, list);

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
        const subject = owner.own ? "you" : owner.name;
        return (
            <p className="py-10 text-haze">
                {list === "followers"
                    ? `Nobody follows ${subject} yet.`
                    : owner.own
                      ? "You do not follow anyone yet."
                      : `${owner.name} does not follow anyone yet.`}
            </p>
        );
    }

    return (
        <ul className="mt-8 grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
            {follows.data.map((person) => (
                <li key={person.id}>
                    <UserLink user={person} />
                </li>
            ))}
        </ul>
    );
}

function FollowButton({
    profile,
    currentUserId,
}: {
    profile: UserProfile;
    currentUserId: number;
}) {
    const toggle = useToggleFollow(profile.id, currentUserId);
    const following = profile.isFollowing;

    return (
        <div>
            <button
                type="button"
                onClick={() => toggle.mutate(following)}
                disabled={toggle.isPending}
                aria-pressed={following}
                className={`group inline-flex h-9 min-w-28 items-center justify-center rounded-sm px-4 text-sm font-semibold transition disabled:opacity-50 ${
                    following
                        ? "text-screen ring-1 ring-white/15 hover:text-alarm hover:ring-alarm/50"
                        : "bg-projector text-salon hover:brightness-110"
                }`}
            >
                {following ? (
                    <>
                        <span className="group-hover:hidden">Following</span>
                        <span className="hidden group-hover:inline">
                            Unfollow
                        </span>
                    </>
                ) : (
                    "Follow"
                )}
            </button>
            {toggle.isError && (
                <p role="alert" className="mt-2 text-sm text-alarm">
                    {toggle.error.message}
                </p>
            )}
        </div>
    );
}

function isTab(value: string | null, tabs: Tab[]): value is Tab {
    return tabs.includes(value as Tab);
}

function ProfileView({ userId, own }: { userId: number; own: boolean }) {
    const { user } = useAuth();
    const profile = useUserProfile(userId);
    const [params, setParams] = useSearchParams();

    if (profile.error instanceof ApiError && profile.error.status === 404) {
        return <NotFoundPage />;
    }

    const name = profile.data?.userName ?? (own ? user!.username : "");
    const owner: Owner = { id: userId, name, own };

    const tabs: { id: Tab; label: string; count?: number }[] = [
        { id: "diary", label: "Diary" },
        { id: "reviews", label: "Reviews" },
        { id: "lists", label: "Lists" },
        ...(own ? [{ id: "watchlist" as const, label: "Watchlist" }] : []),
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
    const requested = params.get("tab");
    const tab: Tab = isTab(
        requested,
        tabs.map((t) => t.id),
    )
        ? requested
        : "diary";

    return (
        <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
            <header className="flex flex-wrap items-end gap-x-6 gap-y-4">
                <UserAvatar
                    userName={name || "?"}
                    imageUrl={profile.data?.profileImageUrl ?? null}
                    className="size-20 text-4xl sm:size-28 sm:text-5xl"
                />
                <div className="min-w-0">
                    <p className="text-sm text-projector">
                        {own ? "Your profile" : "Member"}
                    </p>
                    <h1 className="marquee mt-3 text-6xl wrap-break-word sm:text-8xl">
                        {name}
                    </h1>
                </div>
                {!own && user && profile.data && (
                    <div className="sm:ml-auto">
                        <FollowButton
                            profile={profile.data}
                            currentUserId={user.id}
                        />
                    </div>
                )}
            </header>

            {profile.isError && (
                <ErrorMessage
                    error={profile.error}
                    retry={() => profile.refetch()}
                />
            )}

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
                {tab === "diary" &&
                    (own ? (
                        <OwnDiary owner={owner} />
                    ) : (
                        <UserDiary owner={owner} />
                    ))}
                {tab === "reviews" && <Reviews owner={owner} />}
                {tab === "lists" && <Lists owner={owner} />}
                {tab === "watchlist" && <Watchlist owner={owner} />}
                {(tab === "followers" || tab === "following") && (
                    <FollowList owner={owner} list={tab} />
                )}
            </div>
        </div>
    );
}

export function ProfilePage() {
    // RequireAuth guarantees a user on this route.
    const user = useAuth().user!;
    return <ProfileView userId={user.id} own />;
}

export function UserPage() {
    const { user } = useAuth();
    const [params] = useSearchParams();
    const userId = Number(useParams().userId);

    if (!Number.isInteger(userId) || userId <= 0) {
        return <NotFoundPage />;
    }

    // Your own page lives at /profile, which also shows the watchlist.
    if (user?.id === userId) {
        const search = params.toString();
        return (
            <Navigate to={`/profile${search ? `?${search}` : ""}`} replace />
        );
    }

    // The key resets follow-button state when moving from one user's page to another.
    return <ProfileView key={userId} userId={userId} own={false} />;
}
