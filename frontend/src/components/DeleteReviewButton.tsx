import { useState } from "react";
import { useDeleteReview } from "../lib/queries";

interface DeleteReviewButtonProps {
  reviewId: number;
  onDeleted?: () => void;
  className?: string;
}

// "Delete review" that asks once before deleting. The diary entry that logged the review stays.
export function DeleteReviewButton({ reviewId, onDeleted, className = "" }: DeleteReviewButtonProps) {
  const [confirming, setConfirming] = useState(false);
  const remove = useDeleteReview();

  if (!confirming) {
    return (
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className={`text-sm text-haze underline underline-offset-4 transition hover:text-alarm ${className}`}
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
        className="font-medium text-alarm underline underline-offset-4 disabled:opacity-50"
      >
        {remove.isPending ? "Deleting…" : "Delete"}
      </button>
      <button
        type="button"
        onClick={() => setConfirming(false)}
        disabled={remove.isPending}
        className="text-haze underline underline-offset-4 hover:text-screen"
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
