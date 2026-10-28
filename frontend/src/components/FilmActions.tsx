import { useCallback, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { useReturnFocus } from "../lib/focus";
import {
  useDeleteRating,
  useMarkWatched,
  useMyRating,
  useMyReview,
  useMyStatus,
  useRate,
  useRemoveWatched,
  useSetLiked,
  useToggleWatchlist,
  useWatchlistState,
} from "../lib/queries";
import type { MovieDetails } from "../lib/types";
import { AddToListDialog } from "./AddToListDialog";
import { LogFilmDialog } from "./RatingDialog";
import { StarInput } from "./Stars";

// How long the stars wait after the last change before saving, so arrow keys send one request, not one per step.
export const RATING_SAVE_DELAY_MS = 400;

function ActionToggle({
  pressed,
  onToggle,
  disabled,
  icon,
  children,
  buttonRef,
}: {
  pressed: boolean;
  onToggle: () => void;
  disabled?: boolean;
  icon: ReactNode;
  children: ReactNode;
  buttonRef?: React.Ref<HTMLButtonElement>;
}) {
  return (
    <button
      type="button"
      ref={buttonRef}
      aria-pressed={pressed}
      onClick={onToggle}
      disabled={disabled}
      className={`flex h-16 flex-col items-center justify-center gap-1 rounded-sm text-xs font-medium transition disabled:cursor-not-allowed disabled:opacity-50 ${
        pressed ? "bg-row-raised text-screen ring-1 ring-projector ring-inset" : "text-haze ring-1 ring-rule/20 ring-inset hover:text-screen"
      }`}
    >
      {icon}
      {children}
    </button>
  );
}

function EyeIcon({ filled }: { filled: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={`size-5 ${filled ? "text-projector" : ""}`} aria-hidden="true">
      <path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" fill={filled ? "currentColor" : "none"} />
    </svg>
  );
}

function HeartIcon({ filled }: { filled: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth="2" strokeLinejoin="round" className={`size-5 ${filled ? "text-alarm" : ""}`} aria-hidden="true">
      <path d="M12 20s-7-4.5-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.5-7 10-7 10z" />
    </svg>
  );
}

function BookmarkIcon({ filled }: { filled: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth="2" strokeLinejoin="round" className={`size-5 ${filled ? "text-projector" : ""}`} aria-hidden="true">
      <path d="M6 3h12v18l-6-4-6 4z" />
    </svg>
  );
}

// Stars that save on their own. The picked value shows at once and is sent after a short pause.
function QuickRating({ tmdbId, score, disabled }: { tmdbId: number; score: number | null; disabled: boolean }) {
  const labelId = useId();
  const rate = useRate(tmdbId);
  const remove = useDeleteRating(tmdbId);
  const [draft, setDraft] = useState<number | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pending = useRef<number | null>(null);
  const shown = draft ?? score;
  const { mutate } = rate;

  const flush = useCallback(() => {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    const next = pending.current;
    pending.current = null;
    if (next !== null) {
      // A newer pick may be waiting; only the last one clears the draft.
      mutate(next, { onSettled: () => setDraft((current) => (current === next ? null : current)) });
    }
  }, [mutate]);

  // A pick still waiting when the page changes is saved, not dropped.
  useEffect(() => flush, [flush]);

  function pick(next: number) {
    setDraft(next);
    pending.current = next;
    if (timer.current) {
      clearTimeout(timer.current);
    }
    timer.current = setTimeout(flush, RATING_SAVE_DELAY_MS);
  }

  const error = rate.error ?? remove.error;

  return (
    <div className="text-center">
      <p id={labelId} className="text-sm text-haze">
        Your rating
      </p>
      <div className="mt-2" onBlur={flush}>
        {disabled ? (
          <div className="h-10 sm:h-12" aria-hidden="true" />
        ) : (
          <StarInput value={shown} onChange={pick} labelledBy={labelId} />
        )}
      </div>
      <p className="mt-1 flex h-5 items-center justify-center gap-3 text-sm">
        <span className="text-projector" aria-live="polite">
          {shown !== null && `${shown} / 5`}
        </span>
        {score !== null && draft === null && (
          <button
            type="button"
            onClick={() => remove.mutate()}
            disabled={remove.isPending || rate.isPending}
            className="link-quiet text-xs disabled:opacity-50"
          >
            Remove rating
          </button>
        )}
      </p>
      {error && (
        <p role="alert" className="mt-2 text-sm text-alarm">
          {error.message}
        </p>
      )}
    </div>
  );
}

// The member's one-click actions for a film: watched, like, watchlist and stars, plus the full log dialog
// for a review or another date. Marking watched, liking or rating an unwatched film logs it once; changes on
// a watched film do not add diary entries (JournalService).
export function FilmActions({ movie, className = "" }: { movie: MovieDetails; className?: string }) {
  const tmdbId = movie.id;
  const status = useMyStatus(tmdbId);
  const rating = useMyRating(tmdbId);
  const review = useMyReview(tmdbId);
  const watchlist = useWatchlistState(tmdbId);
  const markWatched = useMarkWatched(tmdbId);
  const setLiked = useSetLiked(tmdbId);
  const unwatch = useRemoveWatched(tmdbId);
  const toggleWatchlist = useToggleWatchlist(tmdbId);
  const [confirmUnwatch, setConfirmUnwatch] = useState(false);
  const watchedRef = useReturnFocus(confirmUnwatch);
  const [logOpen, setLogOpen] = useState(false);
  const [listOpen, setListOpen] = useState(false);
  const confirmTextId = useId();

  const current = status.data ?? null;
  const watched = current !== null;
  const liked = current?.liked === true;
  const onWatchlist = watchlist.data?.onWatchlist ?? false;
  const loading = status.isPending || rating.isPending;
  const changingWatched = markWatched.isPending || unwatch.isPending;
  const error = markWatched.error ?? setLiked.error ?? unwatch.error ?? toggleWatchlist.error;

  function toggleLiked() {
    if (current) {
      setLiked.mutate({ liked: liked ? null : true, rewatched: current.rewatched });
    } else {
      markWatched.mutate(true);
    }
  }

  return (
    <aside aria-label="Your activity for this film" className={className}>
      <div className="grid grid-cols-3 gap-2">
        <ActionToggle
          buttonRef={watchedRef}
          pressed={watched}
          onToggle={() => (watched ? setConfirmUnwatch(true) : markWatched.mutate(null))}
          disabled={loading || changingWatched}
          icon={<EyeIcon filled={watched} />}
        >
          Watched
        </ActionToggle>
        <ActionToggle
          pressed={liked}
          onToggle={toggleLiked}
          disabled={loading || changingWatched}
          icon={<HeartIcon filled={liked} />}
        >
          Liked
        </ActionToggle>
        <ActionToggle
          pressed={onWatchlist}
          onToggle={() => toggleWatchlist.mutate(onWatchlist)}
          disabled={watchlist.isPending || toggleWatchlist.isPending}
          icon={<BookmarkIcon filled={onWatchlist} />}
        >
          Watchlist
        </ActionToggle>
      </div>

      {confirmUnwatch && (
        <div role="group" aria-labelledby={confirmTextId} className="mt-3 rounded-sm bg-salon/60 p-3 text-sm">
          <p id={confirmTextId} className="text-haze">
            Remove from watched? This deletes every diary entry for this film and your review.
          </p>
          <div className="mt-2 flex gap-4">
            <button
              type="button"
              onClick={() => unwatch.mutate(undefined, { onSuccess: () => setConfirmUnwatch(false) })}
              disabled={unwatch.isPending}
              className="link-danger"
            >
              {unwatch.isPending ? "Removing…" : "Remove"}
            </button>
            <button type="button" autoFocus onClick={() => setConfirmUnwatch(false)} disabled={unwatch.isPending} className="link-quiet">
              Keep
            </button>
          </div>
        </div>
      )}

      {error && (
        <p role="alert" className="mt-3 text-sm text-alarm">
          {error.message}
        </p>
      )}

      <div className="mt-5 border-t border-rule/10 pt-4">
        <QuickRating tmdbId={tmdbId} score={rating.data?.score ?? null} disabled={loading} />
      </div>

      <div className="mt-4 grid gap-2">
        <button
          type="button"
          onClick={() => setLogOpen(true)}
          disabled={loading || review.isPending}
          aria-haspopup="dialog"
          className="btn btn-primary"
        >
          {review.data ? "Edit review or log a date" : "Review or log a date"}
        </button>
        <button type="button" onClick={() => setListOpen(true)} aria-haspopup="dialog" className="btn btn-secondary">
          Add to list
        </button>
      </div>

      {logOpen && <LogFilmDialog movie={movie} onClose={() => setLogOpen(false)} />}
      {listOpen && <AddToListDialog movie={movie} onClose={() => setListOpen(false)} />}
    </aside>
  );
}
