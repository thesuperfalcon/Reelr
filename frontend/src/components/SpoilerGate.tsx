import { useEffect, useRef, useState } from "react";

// Hides a review that its author marked as containing spoilers until the reader asks to see it.
// The author always sees their own text.
export function SpoilerGate({
  active,
  className = "mt-3",
  children,
}: {
  active: boolean;
  /** Spacing for the notice that stands in for the text. */
  className?: string;
  children: React.ReactNode;
}) {
  const [revealed, setRevealed] = useState(false);
  const textRef = useRef<HTMLDivElement>(null);

  // The button goes away when pressed, so focus moves to the text it showed instead of being lost.
  useEffect(() => {
    if (revealed) {
      textRef.current?.focus();
    }
  }, [revealed]);

  if (!active) {
    return <>{children}</>;
  }

  if (revealed) {
    return (
      <div ref={textRef} tabIndex={-1} className="outline-none">
        {children}
      </div>
    );
  }

  return (
    <p className={`${className} flex flex-wrap items-center gap-x-3 gap-y-1 rounded-sm border border-dashed border-alarm/40 px-4 py-3 text-sm text-haze`}>
      This review contains spoilers.
      <button
        type="button"
        onClick={() => setRevealed(true)}
        className="link"
      >
        Show review
      </button>
    </p>
  );
}

// Small label next to a review's date, shown to everyone including the author.
export function SpoilerTag() {
  return <span className="rounded-sm px-1.5 py-0.5 text-xs font-medium text-alarm ring-1 ring-alarm/40">Spoilers</span>;
}
