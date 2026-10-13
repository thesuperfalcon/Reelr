import { useState } from "react";
import { useDeleteReview } from "../lib/queries";
import { useReturnFocus } from "../lib/focus";

interface DeleteReviewButtonProps {
  reviewId: number;
  onDeleted?: () => void;
  className?: string;
}

// "Delete review" that asks once before deleting. The diary entry that logged the review stays.
export function DeleteReviewButton({ reviewId, onDeleted, className = "" }: DeleteReviewButtonProps) {
  const [confirming, setConfirming] = useState(false);
  const remove = useDeleteReview();
  const triggerRef = useReturnFocus(confirming);

  if (!confirming) {
    return (
      <button
        ref={triggerRef}
        type="button"
        onClick={() => setConfirming(true)}
        className={`link-quiet text-sm hover:text-alarm ${className}`}
      >
        Delete review
      </button>
    );
  }

  return (
    <span className={`inline-flex flex-wrap items-center gap-x-3 gap-y-1 text-sm ${className}`}>
      <span className="text-haze">Delete your review?</span>
      <button
        type="button"
        onClick={() => remove.mutate(reviewId, { onSuccess: onDeleted })}
        disabled={remove.isPending}
        className="link-danger"
      >
        {remove.isPending ? "Deleting…" : "Delete"}
      </button>
      <button
        type="button"
        autoFocus
        onClick={() => setConfirming(false)}
        disabled={remove.isPending}
        className="link-quiet"
      >
        Keep
      </button>
      {remove.isError && (
        <span role="alert" className="text-alarm">
          {remove.error.message}
        </span>
      )}
    </span>
  );
}
