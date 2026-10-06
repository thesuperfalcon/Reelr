import { useId } from "react";
import { EARLIEST_WATCH_DAY, localDay, today } from "../lib/dates";

function yesterday(): string {
  const date = new Date();
  date.setDate(date.getDate() - 1);
  return localDay(date);
}

// Day picker for a diary entry, with shortcuts for the two most common answers.
export function WatchedOnField({ value, onChange }: { value: string; onChange: (day: string) => void }) {
  const inputId = useId();
  const shortcuts = [
    { label: "Today", day: today() },
    { label: "Yesterday", day: yesterday() },
  ];

  return (
    <div className="flex flex-wrap items-center justify-center gap-2">
      <label htmlFor={inputId} className="sr-only">
        Watched on
      </label>
      <input
        id={inputId}
        type="date"
        value={value}
        min={EARLIEST_WATCH_DAY}
        max={today()}
        required
        // An emptied field falls back to today rather than leaving no date.
        onChange={(event) => onChange(event.target.value || today())}
        className="h-9 rounded-sm bg-salon px-3 text-sm text-screen ring-1 ring-white/15 [color-scheme:dark] focus:outline-none focus-visible:ring-2 focus-visible:ring-projector"
      />
      {shortcuts.map(({ label, day }) => (
        <button
          key={label}
          type="button"
          aria-pressed={value === day}
          onClick={() => onChange(day)}
          className={`h-9 rounded-sm px-3 text-sm font-medium ring-1 transition ${
            value === day ? "bg-row-raised text-screen ring-white/30" : "text-haze ring-white/15 hover:text-screen"
          }`}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
