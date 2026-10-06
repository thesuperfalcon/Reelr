import { useState } from "react";
import { useParams, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { ApiError } from "../lib/api";
import { useDiary, usePersonDetails } from "../lib/queries";
import { releaseYear, tmdbImage } from "../lib/tmdb";
import type { PersonCredit, PersonDetails, SearchMovie } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";

// TMDB dates are plain calendar days, so format them in UTC to avoid shifting a day.
const dateFormat = new Intl.DateTimeFormat("en", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

function parseDay(date: string): Date {
  return new Date(`${date}T00:00:00Z`);
}

function ageBetween(from: string, to: Date): number {
  const born = parseDay(from);
  const age = to.getUTCFullYear() - born.getUTCFullYear();
  const beforeBirthday =
    to.getUTCMonth() < born.getUTCMonth() ||
    (to.getUTCMonth() === born.getUTCMonth() && to.getUTCDate() < born.getUTCDate());
  return beforeBirthday ? age - 1 : age;
}

function asSearchMovie(credit: PersonCredit): SearchMovie {
  return {
    id: credit.tmdbId,
    title: credit.title,
    poster_path: credit.posterPath,
    overview: null,
    release_date: credit.releaseDate,
    vote_average: credit.voteAverage,
  };
}

function LifeFacts({ person }: { person: PersonDetails }) {
  const facts: { term: string; value: string }[] = [];

  if (person.birthday) {
    const age = person.deathday ? null : ageBetween(person.birthday, new Date());
    facts.push({
      term: "Born",
      value:
        dateFormat.format(parseDay(person.birthday)) +
        (age !== null ? ` (age ${age})` : "") +
        (person.placeOfBirth ? ` in ${person.placeOfBirth}` : ""),
    });
  } else if (person.placeOfBirth) {
    facts.push({ term: "Born", value: `in ${person.placeOfBirth}` });
  }

  if (person.deathday) {
    const age = person.birthday ? ageBetween(person.birthday, parseDay(person.deathday)) : null;
    facts.push({
      term: "Died",
      value: dateFormat.format(parseDay(person.deathday)) + (age !== null ? ` (aged ${age})` : ""),
    });
  }

  if (facts.length === 0) {
    return null;
  }

  return (
    <dl className="mt-6 grid gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
      {facts.map((fact) => (
        <div key={fact.term}>
          <dt className="text-haze">{fact.term}</dt>
          <dd className="mt-0.5 text-screen">{fact.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function ExternalLinks({ person }: { person: PersonDetails }) {
  const links = [
    person.imdbId && { label: "IMDb", href: `https://www.imdb.com/name/${person.imdbId}` },
    person.instagramId && { label: "Instagram", href: `https://www.instagram.com/${person.instagramId}` },
    person.twitterId && { label: "X", href: `https://x.com/${person.twitterId}` },
    person.facebookId && { label: "Facebook", href: `https://www.facebook.com/${person.facebookId}` },
    person.homepage && { label: "Website", href: person.homepage },
  ].filter((link): link is { label: string; href: string } => Boolean(link));

  if (links.length === 0) {
    return null;
  }

  return (
    <ul className="mt-6 flex flex-wrap gap-2 text-sm">
      {links.map((link) => (
        <li key={link.label}>
          <a
            href={link.href}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-block rounded-sm px-3 py-1.5 font-medium text-screen ring-1 ring-white/15 transition hover:bg-row"
          >
            {link.label}
          </a>
        </li>
      ))}
    </ul>
  );
}

const BIO_PREVIEW_LENGTH = 600;

function Biography({ text }: { text: string }) {
  const [expanded, setExpanded] = useState(false);
  const long = text.length > BIO_PREVIEW_LENGTH;
  // Cut at the last space so the preview never ends mid-word.
  const shown = !long || expanded ? text : `${text.slice(0, text.lastIndexOf(" ", BIO_PREVIEW_LENGTH))}…`;

  return (
    <div className="mt-6 max-w-prose">
      <div className="space-y-3 text-screen/90">
        {shown.split(/\n+/).map((paragraph, i) => (
          <p key={i}>{paragraph}</p>
        ))}
      </div>
      {long && (
        <button
          type="button"
          onClick={() => setExpanded(!expanded)}
          aria-expanded={expanded}
          className="mt-3 text-sm font-medium text-projector underline underline-offset-4"
        >
          {expanded ? "Show less" : "Read more"}
        </button>
      )}
    </div>
  );
}

function CreditCard({ credit, watched }: { credit: PersonCredit; watched: boolean }) {
  const year = releaseYear(credit.releaseDate);

  return (
    <li>
      <div className="relative">
        <Poster movie={asSearchMovie(credit)} size="w342" />
        {watched && (
          <span
            className="pointer-events-none absolute right-1.5 top-1.5 flex size-6 items-center justify-center rounded-full bg-projector text-salon shadow"
            title="You have watched this"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" className="size-3.5" aria-hidden="true">
              <path d="M5 12l5 5 9-10" />
            </svg>
            <span className="sr-only">Watched</span>
          </span>
        )}
      </div>
      <p className="mt-2 truncate text-sm font-medium" title={credit.title ?? undefined}>
        {credit.title}
      </p>
      <p className="truncate text-xs text-haze" title={credit.roles.join(" / ") || undefined}>
        {year ?? "Upcoming"}
        {credit.roles.length > 0 && ` · ${credit.roles.join(" / ")}`}
      </p>
    </li>
  );
}

type Sort = "newest" | "oldest" | "popular" | "rating";

const sorts: { id: Sort; label: string }[] = [
  { id: "newest", label: "Newest first" },
  { id: "oldest", label: "Oldest first" },
  { id: "popular", label: "Most popular" },
  { id: "rating", label: "TMDB rating" },
];

function isSort(value: string | null): value is Sort {
  return sorts.some((s) => s.id === value);
}

// The API returns newest first with unreleased films on top; other orders sort a copy.
function sortCredits(credits: PersonCredit[], sort: Sort): PersonCredit[] {
  switch (sort) {
    case "newest":
      return credits;
    case "oldest":
      return [...credits].reverse();
    case "popular":
      return [...credits].sort((a, b) => b.voteCount - a.voteCount);
    case "rating":
      // Films nobody has voted on go last instead of counting as 0.
      return [...credits].sort((a, b) => (b.voteCount > 0 ? b.voteAverage : -1) - (a.voteCount > 0 ? a.voteAverage : -1));
  }
}

// Element ids cannot hold spaces, and departments such as "Costume & Make-Up" do.
function tabId(department: string): string {
  return `dept-${department.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;
}

function departmentsOf(credits: PersonCredit[], knownFor: string | null): { name: string; count: number }[] {
  const counts = new Map<string, number>();
  for (const credit of credits) {
    counts.set(credit.department, (counts.get(credit.department) ?? 0) + 1);
  }

  // The known-for department leads and appearances as themselves trail; the rest go by size.
  const rank = (name: string) => (name === knownFor ? 0 : name === "Appearances" ? 2 : 1);
  return [...counts]
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => rank(a.name) - rank(b.name) || b.count - a.count);
}

function Filmography({ person, watched }: { person: PersonDetails; watched: Set<number> }) {
  const [params, setParams] = useSearchParams();
  const departments = departmentsOf(person.credits, person.knownForDepartment);
  const requestedDepartment = params.get("dept");
  const department = departments.some((d) => d.name === requestedDepartment)
    ? requestedDepartment!
    : departments[0]?.name;
  const requestedSort = params.get("sort");
  const sort: Sort = isSort(requestedSort) ? requestedSort : "newest";

  if (!department) {
    return <p className="mt-16 text-haze">TMDB lists no films for {person.name}.</p>;
  }

  function update(next: { dept?: string; sort?: Sort }) {
    const dept = next.dept ?? department;
    const nextSort = next.sort ?? sort;
    setParams(
      {
        ...(dept !== departments[0].name && { dept }),
        ...(nextSort !== "newest" && { sort: nextSort }),
      },
      { replace: true },
    );
  }

  const credits = sortCredits(
    person.credits.filter((c) => c.department === department),
    sort,
  );

  return (
    <section aria-labelledby="filmography-title" className="mt-16">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h2 id="filmography-title" className="marquee text-3xl">
          Filmography
        </h2>
        <label className="flex items-center gap-2 text-sm text-haze">
          Sort by
          <select
            value={sort}
            onChange={(event) => update({ sort: event.target.value as Sort })}
            className="rounded-sm bg-row px-2 py-1.5 text-screen ring-1 ring-white/15 focus:outline-2 focus:outline-projector"
          >
            {sorts.map((s) => (
              <option key={s.id} value={s.id}>
                {s.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div
        role="tablist"
        aria-label="Departments"
        className="mt-6 flex gap-6 overflow-x-auto overflow-y-hidden border-b border-white/5 [&::-webkit-scrollbar]:hidden"
      >
        {departments.map(({ name, count }) => (
          <button
            key={name}
            type="button"
            role="tab"
            id={tabId(name)}
            aria-selected={department === name}
            aria-controls="filmography-panel"
            onClick={() => update({ dept: name })}
            className={`-mb-px shrink-0 border-b-2 pb-3 text-sm font-medium transition-colors ${
              department === name ? "border-projector text-screen" : "border-transparent text-haze hover:text-screen"
            }`}
          >
            {name}
            <span className="ml-1.5 text-haze">{count}</span>
          </button>
        ))}
      </div>

      <ul
        role="tabpanel"
        id="filmography-panel"
        aria-labelledby={tabId(department)}
        className="mt-6 grid grid-cols-3 gap-x-3 gap-y-6 sm:grid-cols-4 md:grid-cols-6 lg:gap-x-4"
      >
        {credits.map((credit) => (
          <CreditCard key={credit.tmdbId} credit={credit} watched={watched.has(credit.tmdbId)} />
        ))}
      </ul>
    </section>
  );
}

function KnownFor({ credits, watched }: { credits: PersonCredit[]; watched: Set<number> }) {
  if (credits.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="known-for-title" className="mt-16">
      <h2 id="known-for-title" className="marquee text-3xl">
        Known for
      </h2>
      <ul className="mt-5 grid grid-cols-4 gap-x-3 gap-y-6 md:grid-cols-8 lg:gap-x-4">
        {credits.map((credit) => (
          <CreditCard key={credit.tmdbId} credit={credit} watched={watched.has(credit.tmdbId)} />
        ))}
      </ul>
    </section>
  );
}

export function PersonPage() {
  const personId = Number(useParams().personId);
  const { user } = useAuth();
  const details = usePersonDetails(personId);
  // The diary marks films the logged-in user has seen.
  const diary = useDiary(user !== null);

  if (!Number.isInteger(personId) || personId <= 0 || (details.error instanceof ApiError && details.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (details.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading person" />
      </div>
    );
  }

  if (details.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={details.error} retry={() => details.refetch()} />
      </div>
    );
  }

  const person = details.data;
  const photo = tmdbImage(person.profilePath, "w500");
  const watched = new Set(user ? (diary.data ?? []).map((entry) => entry.tmdbId) : []);
  const films = new Set(person.credits.filter((c) => c.department !== "Appearances").map((c) => c.tmdbId));
  const watchedCount = [...films].filter((id) => watched.has(id)).length;

  return (
    <article className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="grid gap-8 md:grid-cols-[16rem_1fr] md:gap-12">
        <div className="w-36 sm:w-48 md:w-full">
          {photo ? (
            <img src={photo} alt={`Photo of ${person.name}`} className="aspect-[2/3] w-full rounded-sm object-cover ring-1 ring-white/10" />
          ) : (
            <div className="aspect-[2/3] w-full rounded-sm bg-row" aria-hidden="true" />
          )}
        </div>

        <div className="min-w-0">
          {person.knownForDepartment && <p className="text-sm text-projector">{person.knownForDepartment}</p>}
          <h1 className="marquee mt-3 text-6xl sm:text-8xl">{person.name}</h1>

          {user && films.size > 0 && diary.isSuccess && (
            <p className="mt-4 text-sm text-haze">
              You have watched <span className="font-semibold text-screen">{watchedCount}</span> of {films.size}{" "}
              {films.size === 1 ? "film" : "films"}.
            </p>
          )}

          <LifeFacts person={person} />
          {person.biography && <Biography text={person.biography} />}
          <ExternalLinks person={person} />
        </div>
      </div>

      <KnownFor credits={person.knownFor} watched={watched} />
      <Filmography person={person} watched={watched} />
    </article>
  );
}
