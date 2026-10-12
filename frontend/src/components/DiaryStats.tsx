import { useId, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { Link } from "react-router";
import { useStatsDay, useUserStats, type UserStats } from "../lib/stats";
import { tmdbImage } from "../lib/tmdb";
import { Stars } from "./Stars";
import { ErrorMessage, Loading } from "./Status";

// Dates from the API are calendar days ("YYYY-MM-DD"). They are handled as UTC midnight throughout, so no time
// zone can move a film to the day before or after.
const DAY_MS = 24 * 60 * 60 * 1000;
const toTime = (day: string) => Date.parse(`${day}T00:00:00Z`);
const toDay = (time: number) => new Date(time).toISOString().slice(0, 10);

const longDate = new Intl.DateTimeFormat(undefined, { dateStyle: "full", timeZone: "UTC" });
const shortMonth = new Intl.DateTimeFormat(undefined, { month: "short", timeZone: "UTC" });
const longMonth = new Intl.DateTimeFormat(undefined, { month: "long", year: "numeric", timeZone: "UTC" });
const weekdayName = new Intl.DateTimeFormat(undefined, { weekday: "long", timeZone: "UTC" });
// 2023-01-01 was a Sunday, so day n of that week is weekday n (0 = Sunday).
const weekdayLabel = (weekday: number) => weekdayName.format(Date.UTC(2023, 0, 1 + weekday));

const films = (count: number) => `${count} ${count === 1 ? "film" : "films"}`;

// Darker means more films, like GitHub's graph, in the theme's accent colour.
function level(count: number) {
  if (count === 0) return "bg-row";
  if (count === 1) return "bg-projector/30";
  if (count === 2) return "bg-projector/55";
  if (count === 3) return "bg-projector/80";
  return "bg-projector";
}

interface Cell {
  day: string;
  count: number;
}

// Weeks as columns from Sunday to Saturday, like GitHub; null pads the days before the first and after the last.
function buildWeeks(stats: UserStats): (Cell | null)[][] {
  const counts = new Map(stats.days.map((d) => [d.date, d.count]));
  const first = toTime(stats.from);
  const last = toTime(stats.to);
  const start = first - new Date(first).getUTCDay() * DAY_MS;
  const weeks: (Cell | null)[][] = [];

  for (let weekStart = start; weekStart <= last; weekStart += 7 * DAY_MS) {
    const week: (Cell | null)[] = [];
    for (let i = 0; i < 7; i++) {
      const time = weekStart + i * DAY_MS;
      const day = toDay(time);
      week.push(time < first || time > last ? null : { day, count: counts.get(day) ?? 0 });
    }
    weeks.push(week);
  }

  return weeks;
}

function Graph({
  stats,
  selected,
  onSelect,
  labelledBy,
}: {
  stats: UserStats;
  selected: string | null;
  onSelect: (day: string) => void;
  labelledBy: string;
}) {
  const weeks = useMemo(() => buildWeeks(stats), [stats]);
  const helpId = useId();
  const container = useRef<HTMLDivElement>(null);
  // One day is reachable with Tab; the arrow keys move between days (roving tab index).
  const [focusDay, setFocusDay] = useState<string | null>(null);
  const current = focusDay && focusDay >= stats.from && focusDay <= stats.to ? focusDay : (selected ?? stats.to);

  function move(event: KeyboardEvent<HTMLDivElement>) {
    const steps: Record<string, number> = { ArrowUp: -1, ArrowDown: 1, ArrowLeft: -7, ArrowRight: 7 };
    let next: string;

    if (event.key in steps) next = toDay(toTime(current) + steps[event.key] * DAY_MS);
    else if (event.key === "Home") next = stats.from;
    else if (event.key === "End") next = stats.to;
    else return;

    event.preventDefault();
    if (next < stats.from || next > stats.to) return;
    setFocusDay(next);
    container.current?.querySelector<HTMLButtonElement>(`[data-day="${next}"]`)?.focus();
  }

  // A month's name goes above the week its first day falls in.
  const monthLabels = weeks.map((week) => {
    const firstOfMonth = week.find((cell) => cell?.day.endsWith("-01"));
    return firstOfMonth ? shortMonth.format(toTime(firstOfMonth.day)) : "";
  });

  return (
    <div className="mt-4 overflow-x-auto pb-2">
      <p id={helpId} className="sr-only">
        Each button is a day. Use the arrow keys to move between days and Enter to see the films of a day.
      </p>
      <div className="inline-flex flex-col gap-1">
        <div className="flex gap-[3px] pl-0 text-[10px] text-haze" aria-hidden="true">
          {monthLabels.map((label, i) => (
            <span key={i} className="w-[11px] overflow-visible whitespace-nowrap">
              {label}
            </span>
          ))}
        </div>
        <div
          ref={container}
          role="group"
          aria-labelledby={labelledBy}
          aria-describedby={helpId}
          onKeyDown={move}
          className="flex gap-[3px]"
        >
          {weeks.map((week, w) => (
            <div key={w} className="flex flex-col gap-[3px]">
              {week.map((cell, d) =>
                cell ? (
                  <button
                    key={cell.day}
                    type="button"
                    data-day={cell.day}
                    tabIndex={cell.day === current ? 0 : -1}
                    aria-pressed={cell.day === selected}
                    aria-label={`${films(cell.count)} on ${longDate.format(toTime(cell.day))}`}
                    title={`${films(cell.count)}, ${longDate.format(toTime(cell.day))}`}
                    onClick={() => {
                      setFocusDay(cell.day);
                      onSelect(cell.day);
                    }}
                    className={`size-[11px] rounded-[2px] ${level(cell.count)} ring-projector focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-projector aria-pressed:ring-2`}
                  />
                ) : (
                  <span key={`pad-${d}`} className="size-[11px]" aria-hidden="true" />
                ),
              )}
            </div>
          ))}
        </div>
        <div className="mt-1 flex items-center justify-end gap-1 text-[10px] text-haze" aria-hidden="true">
          Fewer
          {[0, 1, 2, 3, 4].map((count) => (
            <span key={count} className={`size-[11px] rounded-[2px] ${level(count)}`} />
          ))}
          More
        </div>
      </div>
    </div>
  );
}

function Summary({ stats, period }: { stats: UserStats; period: string }) {
  const items: [string, string][] = [
    [period, films(stats.total)],
    ["Longest streak", `${stats.longestStreak} ${stats.longestStreak === 1 ? "day" : "days"}`],
    ...(stats.currentStreak == null
      ? []
      : ([["Current streak", `${stats.currentStreak} ${stats.currentStreak === 1 ? "day" : "days"}`]] as [string, string][])),
    ...(stats.busiestWeekday == null ? [] : ([["Busiest day", weekdayLabel(stats.busiestWeekday)]] as [string, string][])),
  ];

  return (
    <dl className="mt-4 flex flex-wrap gap-x-8 gap-y-3">
      {items.map(([label, value]) => (
        <div key={label}>
          <dt className="text-xs text-haze">{label}</dt>
          <dd className="marquee text-2xl">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function DayFilms({ userId, day }: { userId: number; day: string }) {
  const entries = useStatsDay(userId, day);
  const date = longDate.format(toTime(day));

  return (
    <section aria-live="polite" className="mt-4 rounded-sm bg-row p-4">
      <h3 className="text-sm font-semibold">{date}</h3>
      {entries.isPending ? (
        <Loading label="Loading the films of the day" />
      ) : entries.isError ? (
        <ErrorMessage error={entries.error} retry={() => entries.refetch()} />
      ) : entries.data.length === 0 ? (
        <p className="mt-2 text-sm text-haze">No films logged this day.</p>
      ) : (
        <ul className="mt-3 flex flex-wrap gap-4">
          {entries.data.map((entry) => {
            const poster = tmdbImage(entry.posterUrl, "w185");
            return (
              <li key={entry.id}>
                <Link to={`/movie/${entry.tmdbId}`} className="flex items-center gap-3 text-sm hover:text-projector">
                  {poster ? (
                    <img src={poster} alt="" className="h-16 w-11 rounded-sm object-cover" />
                  ) : (
                    <span className="h-16 w-11 rounded-sm bg-row-raised" aria-hidden="true" />
                  )}
                  <span>
                    <span className="block font-medium">{entry.title}</span>
                    {entry.rating != null && <Stars score={entry.rating} className="h-3" />}
                    {entry.rewatched && <span className="block text-xs text-haze">Rewatch</span>}
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function Months({ stats }: { stats: UserStats }) {
  const most = Math.max(1, ...stats.months.map((m) => m.count));

  return (
    <section aria-label="Films per month" className="mt-8">
      <h3 className="text-sm font-semibold">Films per month</h3>
      <ol className="mt-3 flex h-36 items-end gap-1.5">
        {stats.months.map((month) => {
          const time = Date.UTC(month.year, month.month - 1, 1);
          const rating = month.averageRating == null ? "no ratings" : `average rating ${month.averageRating}`;
          return (
            <li
              key={`${month.year}-${month.month}`}
              className="flex h-full min-w-6 flex-1 flex-col items-center justify-end gap-1"
              aria-label={`${longMonth.format(time)}: ${films(month.count)}, ${rating}`}
            >
              <span className="text-[10px] text-haze" aria-hidden="true">
                {month.averageRating == null ? "" : `★${month.averageRating.toFixed(1)}`}
              </span>
              <span
                className="w-full rounded-t-[2px] bg-projector/70"
                style={{ height: `${(month.count / most) * 100}%`, minHeight: month.count > 0 ? 3 : 0 }}
                title={`${longMonth.format(time)}: ${films(month.count)}, ${rating}`}
                aria-hidden="true"
              />
              <span className="text-[10px] text-haze" aria-hidden="true">
                {shortMonth.format(time)}
              </span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

// A year of the member's diary at the top of the profile, like GitHub's contribution graph. Shown only to those
// who may see the diary; the API answers 404 to anyone else.
export function DiaryStats({ userId, own }: { userId: number; own: boolean }) {
  const [year, setYear] = useState<number | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const stats = useUserStats(userId, year);
  const headingId = useId();

  if (stats.isPending) return <Loading label="Loading film stats" />;
  if (stats.isError) return null;

  const data = stats.data;
  const period = year == null ? "Last 12 months" : String(year);
  const choices: (number | null)[] = [null, ...data.years];

  return (
    <section aria-labelledby={headingId} className="mt-10 border-t border-rule/5 pt-8">
      <div className="flex flex-wrap items-baseline justify-between gap-4">
        <h2 id={headingId} className="marquee text-3xl">
          {films(data.total)} {year == null ? "in the last year" : `in ${year}`}
        </h2>
        {data.years.length > 0 && (
          <div role="group" aria-label="Period" className="flex flex-wrap gap-2">
            {choices.map((choice) => (
              <button
                key={choice ?? "last"}
                type="button"
                aria-pressed={choice === year}
                onClick={() => {
                  setYear(choice);
                  setSelected(null);
                }}
                className="rounded-sm px-3 py-1 text-sm ring-1 ring-rule/15 transition hover:bg-row aria-pressed:bg-projector aria-pressed:text-salon aria-pressed:ring-projector"
              >
                {choice ?? "Last 12 months"}
              </button>
            ))}
          </div>
        )}
      </div>

      {data.total === 0 ? (
        <p className="mt-4 text-sm text-haze">
          {own ? "No films logged in this period. Rate a film to fill the graph." : "No films logged in this period."}
        </p>
      ) : (
        <>
          <Summary stats={data} period={period} />
          <Graph stats={data} selected={selected} onSelect={setSelected} labelledBy={headingId} />
          {selected && <DayFilms userId={userId} day={selected} />}
          <Months stats={data} />
        </>
      )}
    </section>
  );
}
