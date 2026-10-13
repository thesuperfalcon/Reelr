import { useEffect, useRef, useState, type ReactNode } from "react";
import { tmdbImage } from "../lib/tmdb";

interface DialogProps {
  /** Id of the dialog's heading. */
  labelledBy: string;
  onClose: () => void;
  /** Width, e.g. "w-[min(28rem,calc(100vw-2rem))]". The default is 36rem. */
  className?: string;
  children: ReactNode;
}

// A modal dialog. showModal gives the blurred backdrop, the focus trap and Escape to close; a click on the
// backdrop closes it too. The parent closes it by unmounting it, which the browser does not treat as closing,
// so focus is put back on the control that opened it here (WCAG 2.4.3).
export function Dialog({ labelledBy, onClose, className = "", children }: DialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  // Read during the first render, before the dialog takes focus.
  const [opener] = useState(() => document.activeElement);

  useEffect(() => {
    dialogRef.current?.showModal();
    return () => {
      // The opener is gone when saving moved to another page; focus is then the page's business.
      if (opener instanceof HTMLElement && opener.isConnected) {
        opener.focus();
      }
    };
  }, [opener]);

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={labelledBy}
      onClose={onClose}
      // A click on the backdrop lands on the dialog element itself.
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className={`dialog-panel ${className}`}
    >
      {children}
    </dialog>
  );
}

// A film dialog's heading: small poster, what the dialog does, and the film's title.
export function FilmDialogHeader({
  titleId,
  label,
  title,
  year,
  posterPath,
}: {
  titleId: string;
  label: string;
  title: string | null;
  year?: string | null;
  posterPath: string | null;
}) {
  const poster = tmdbImage(posterPath, "w185");

  return (
    <div className="flex items-center gap-4">
      {poster ? (
        <img src={poster} alt="" className="aspect-[2/3] w-14 shrink-0 rounded-sm object-cover ring-1 ring-rule/10" />
      ) : (
        <div className="aspect-[2/3] w-14 shrink-0 rounded-sm bg-row-raised" aria-hidden="true" />
      )}
      <div className="min-w-0">
        <p className="eyebrow">{label}</p>
        <h2 id={titleId} className="marquee mt-1 text-3xl">
          {title}
          {year && <span className="ml-2 text-xl text-haze">{year}</span>}
        </h2>
      </div>
    </div>
  );
}
