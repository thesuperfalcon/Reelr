import type { ReactNode } from "react";
import { ApiError } from "../lib/api";

// A slowly turning film reel. Still when the visitor asks for less motion.
function ReelIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" className="size-5 animate-spin [animation-duration:2.4s]" aria-hidden="true">
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="1.5" />
      <circle cx="12" cy="6.5" r="2" />
      <circle cx="12" cy="17.5" r="2" />
      <circle cx="6.5" cy="12" r="2" />
      <circle cx="17.5" cy="12" r="2" />
    </svg>
  );
}

export function Loading({ label = "Loading" }: { label?: string }) {
  return (
    <p role="status" className="flex items-center gap-3 py-10 text-sm text-haze">
      <ReelIcon />
      {label}…
    </p>
  );
}

export function ErrorMessage({ error, retry }: { error: unknown; retry?: () => void }) {
  const message =
    error instanceof ApiError && error.status >= 500
      ? "The film service is not answering. Try again in a moment."
      : error instanceof Error
        ? error.message
        : "Something went wrong.";

  return (
    <div role="alert" className="my-8 flex flex-wrap items-center gap-x-6 gap-y-3 rounded-sm border-l-2 border-alarm bg-row px-4 py-3">
      <p className="text-sm text-screen">
        <span className="font-semibold text-alarm">Could not load this. </span>
        {message}
      </p>
      {retry && (
        <button type="button" onClick={retry} className="btn btn-sm btn-secondary">
          Try again
        </button>
      )}
    </div>
  );
}

// What a section shows when it has nothing yet, with the next step when there is one.
export function EmptyState({ children, action, className = "mt-8" }: { children: ReactNode; action?: ReactNode; className?: string }) {
  return (
    <div className={`rounded-sm border border-dashed border-rule/15 px-5 py-8 text-center sm:px-8 ${className}`}>
      <div className="mx-auto max-w-prose text-haze">{children}</div>
      {action && <div className="mt-5 flex flex-wrap justify-center gap-3">{action}</div>}
    </div>
  );
}

// Grey poster shapes in the same grid as the films they stand in for, so the page does not jump when they load.
export function PosterGridSkeleton({ count = 12, label, className = "mt-5" }: { count?: number; label: string; className?: string }) {
  return (
    <div role="status" className={className}>
      <span className="sr-only">{label}…</span>
      <ul className="grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6 lg:gap-4" aria-hidden="true">
        {Array.from({ length: count }, (_, i) => (
          <li key={i} className="skeleton aspect-[2/3]" />
        ))}
      </ul>
    </div>
  );
}

// Rows of avatar, text and poster shapes, for feeds and other lists.
export function RowsSkeleton({ count = 4, label }: { count?: number; label: string }) {
  return (
    <div role="status" className="mt-2">
      <span className="sr-only">{label}…</span>
      <ul className="divide-y divide-rule/5" aria-hidden="true">
        {Array.from({ length: count }, (_, i) => (
          <li key={i} className="flex gap-4 py-5">
            <div className="skeleton size-10 shrink-0 rounded-full" />
            <div className="flex-1 space-y-2.5 pt-1">
              <div className="skeleton h-3.5 w-3/5" />
              <div className="skeleton h-3 w-2/5" />
            </div>
            <div className="skeleton aspect-[2/3] w-12 shrink-0 sm:w-14" />
          </li>
        ))}
      </ul>
    </div>
  );
}
