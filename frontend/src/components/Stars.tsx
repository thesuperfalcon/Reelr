import { useState, type KeyboardEvent, type MouseEvent } from "react";

export const STAR_PATH = "M12 2.5l2.94 5.96 6.56.95-4.75 4.63 1.12 6.54L12 17.5l-5.87 3.08 1.12-6.54L2.5 9.41l6.56-.95z";

function StarRow({ className }: { className: string }) {
  return (
    <span className={`flex ${className}`}>
      {[0, 1, 2, 3, 4].map((i) => (
        <svg key={i} viewBox="0 0 24 24" fill="currentColor" className="aspect-square h-full shrink-0" aria-hidden="true">
          <path d={STAR_PATH} />
        </svg>
      ))}
    </span>
  );
}

// Five stars filled to `score` out of 5. Half steps fill half a star.
function StarFill({ score, className = "" }: { score: number; className?: string }) {
  return (
    <span className={`relative inline-flex ${className}`}>
      <StarRow className="h-full text-white/15" />
      <span className="absolute inset-y-0 left-0 overflow-hidden" style={{ width: `${(score / 5) * 100}%` }}>
        <StarRow className="h-full text-projector" />
      </span>
    </span>
  );
}

function formatScore(score: number): string {
  return `${score} out of 5 stars`;
}

export function Stars({ score, className = "h-4" }: { score: number; className?: string }) {
  return (
    <span role="img" aria-label={formatScore(score)} title={formatScore(score)} className="inline-flex">
      <StarFill score={score} className={className} />
    </span>
  );
}

interface StarInputProps {
  value: number | null;
  onChange: (score: number) => void;
  labelledBy: string;
}

const MIN = 0.5;
const MAX = 5;

function clamp(score: number) {
  return Math.min(MAX, Math.max(MIN, score));
}

// Half-star picker. Pointer hover previews, click commits; arrow keys step by half a star.
export function StarInput({ value, onChange, labelledBy }: StarInputProps) {
  const [hover, setHover] = useState<number | null>(null);
  const shown = hover ?? value ?? 0;

  function scoreAt(event: MouseEvent<HTMLDivElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    const fraction = (event.clientX - rect.left) / rect.width;
    return clamp(Math.ceil(fraction * 10) / 2);
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const current = value ?? 0;
    const next: Record<string, number> = {
      ArrowRight: current + 0.5,
      ArrowUp: current + 0.5,
      ArrowLeft: current - 0.5,
      ArrowDown: current - 0.5,
      Home: MIN,
      End: MAX,
    };

    if (event.key in next) {
      event.preventDefault();
      onChange(clamp(next[event.key]));
    }
  }

  return (
    <div
      role="slider"
      tabIndex={0}
      aria-labelledby={labelledBy}
      aria-valuemin={MIN}
      aria-valuemax={MAX}
      aria-valuenow={value ?? undefined}
      aria-valuetext={value ? formatScore(value) : "Not rated"}
      onKeyDown={onKeyDown}
      onPointerMove={(event) => setHover(scoreAt(event))}
      onPointerLeave={() => setHover(null)}
      onClick={(event) => onChange(scoreAt(event))}
      className="inline-flex cursor-pointer rounded-sm"
    >
      <StarFill score={shown} className="h-10 sm:h-12" />
    </div>
  );
}
