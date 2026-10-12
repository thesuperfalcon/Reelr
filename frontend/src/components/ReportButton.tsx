import { useEffect, useId, useRef, useState } from "react";
import { ApiError } from "../lib/api";
import { REPORT_NOTE_MAX_LENGTH, reportReasons, useReport, type ReportReason, type ReportTargetType } from "../lib/moderation";
import { FormError } from "./Form";

interface ReportButtonProps {
  targetType: ReportTargetType;
  targetId: number;
  /** What is being reported, for the dialog title, e.g. "this review". */
  what: string;
  className?: string;
}

// A quiet "Report" link that opens a dialog to report a review, comment or list to the admins.
export function ReportButton({ targetType, targetId, what, className = "" }: ReportButtonProps) {
  const [open, setOpen] = useState(false);
  const [done, setDone] = useState(false);

  if (done) {
    return <span className={`text-haze ${className}`}>Reported</span>;
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-haspopup="dialog"
        className={`text-haze underline underline-offset-4 hover:text-alarm ${className}`}
      >
        Report
      </button>
      {open && (
        <ReportDialog
          targetType={targetType}
          targetId={targetId}
          what={what}
          onClose={() => setOpen(false)}
          onReported={() => {
            setOpen(false);
            setDone(true);
          }}
        />
      )}
    </>
  );
}

function ReportDialog({
  targetType,
  targetId,
  what,
  onClose,
  onReported,
}: {
  targetType: ReportTargetType;
  targetId: number;
  what: string;
  onClose: () => void;
  onReported: () => void;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const noteId = useId();
  const [reason, setReason] = useState<ReportReason | null>(null);
  const [note, setNote] = useState("");
  const report = useReport();

  // showModal gives the blurred backdrop, focus trap and Escape to close.
  useEffect(() => {
    dialogRef.current?.showModal();
  }, []);

  function submit() {
    if (!reason) return;
    report.mutate(
      { targetType, targetId, reason, note: note.trim() },
      {
        onSuccess: onReported,
        // Reporting the same thing twice is already done, which is what the user wanted.
        onError: (error) => error instanceof ApiError && error.status === 409 && onReported(),
      },
    );
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      onClose={onClose}
      onClick={(event) => event.target === event.currentTarget && onClose()}
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-md bg-row p-0 text-screen shadow-2xl ring-1 ring-white/10 backdrop:bg-salon/60 backdrop:backdrop-blur-md"
    >
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        className="p-6"
      >
        <h2 id={titleId} className="marquee text-3xl">
          Report {what}
        </h2>
        <p className="mt-2 text-sm text-haze">The admins see your report. The author is not told who reported it.</p>

        <fieldset className="mt-6">
          <legend className="text-sm font-medium">What is wrong?</legend>
          <div className="mt-3 space-y-2">
            {reportReasons.map((option) => (
              <label key={option.value} className="flex items-center gap-3 text-sm">
                <input
                  type="radio"
                  name="report-reason"
                  value={option.value}
                  checked={reason === option.value}
                  onChange={() => setReason(option.value)}
                  className="size-4 accent-projector"
                />
                {option.label}
              </label>
            ))}
          </div>
        </fieldset>

        <label htmlFor={noteId} className="mt-6 block text-sm font-medium">
          Anything the admins should know? (optional)
        </label>
        <textarea
          id={noteId}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          maxLength={REPORT_NOTE_MAX_LENGTH}
          rows={3}
          className="mt-2 block w-full resize-y rounded-sm bg-salon px-3 py-2 text-screen ring-1 ring-edge focus:outline-none focus-visible:ring-2 focus-visible:ring-projector"
        />

        <div className="mt-4">
          <FormError error={report.error instanceof ApiError && report.error.status === 409 ? null : report.error} />
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-sm px-4 py-2 text-sm font-medium text-haze hover:text-screen">
            Cancel
          </button>
          <button
            type="submit"
            disabled={!reason || report.isPending}
            className="rounded-sm bg-alarm px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-60"
          >
            {report.isPending ? "Sending…" : "Send report"}
          </button>
        </div>
      </form>
    </dialog>
  );
}
