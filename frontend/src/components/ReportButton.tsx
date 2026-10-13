import { useId, useState } from "react";
import { ApiError } from "../lib/api";
import { REPORT_NOTE_MAX_LENGTH, reportReasons, useReport, type ReportReason, type ReportTargetType } from "../lib/moderation";
import { Dialog } from "./Dialog";
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
        className={`link-quiet hover:text-alarm ${className}`}
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
  const titleId = useId();
  const noteId = useId();
  const [reason, setReason] = useState<ReportReason | null>(null);
  const [note, setNote] = useState("");
  const report = useReport();

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
    <Dialog labelledBy={titleId} onClose={onClose} className="w-[min(32rem,calc(100vw-2rem))]">
      <form
        method="dialog"
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
        className="p-6"
      >
        <h2 id={titleId} className="heading-section">
          Report {what}
        </h2>
        <p className="mt-2 text-sm text-haze">The admins see your report. The author is not told who reported it.</p>

        <fieldset className="mt-6">
          <legend className="label">What is wrong?</legend>
          <div className="mt-3 space-y-2">
            {reportReasons.map((option) => (
              <label key={option.value} className="flex min-h-8 items-center gap-3 text-sm">
                <input
                  type="radio"
                  name="report-reason"
                  value={option.value}
                  checked={reason === option.value}
                  onChange={() => setReason(option.value)}
                  className="check"
                />
                {option.label}
              </label>
            ))}
          </div>
        </fieldset>

        <label htmlFor={noteId} className="label mt-6">
          Anything the admins should know? (optional)
        </label>
        <textarea
          id={noteId}
          value={note}
          onChange={(event) => setNote(event.target.value)}
          maxLength={REPORT_NOTE_MAX_LENGTH}
          rows={3}
          className="input mt-2 block resize-y"
        />

        <div className="mt-4">
          <FormError error={report.error instanceof ApiError && report.error.status === 409 ? null : report.error} />
        </div>

        <div className="dialog-actions">
          <button type="button" onClick={onClose} className="btn btn-secondary">
            Cancel
          </button>
          <button
            type="submit"
            disabled={!reason || report.isPending}
            className="btn btn-danger"
          >
            {report.isPending ? "Sending…" : "Send report"}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
