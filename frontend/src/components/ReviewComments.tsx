import { useEffect, useId, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/auth-context";
import { fullFormat, timeAgo } from "../lib/activity";
import {
  COMMENT_MAX_LENGTH,
  useAddComment,
  useDeleteComment,
  useReviewComments,
  useUpdateComment,
} from "../lib/queries";
import type { Review, ReviewComment } from "../lib/types";
import { FormError } from "./Form";
import { ErrorMessage, Loading } from "./Status";
import { UserLink } from "./UserAvatar";

const textareaClass =
  "mt-2 block w-full resize-y rounded-sm bg-row px-3 py-2.5 text-screen focus:bg-row-raised focus:outline-2 focus:outline-projector";

function CommentForm({
  initial = "",
  submitLabel,
  pending,
  error,
  onSubmit,
  onCancel,
}: {
  initial?: string;
  submitLabel: string;
  pending: boolean;
  error: unknown;
  onSubmit: (text: string) => void;
  onCancel?: () => void;
}) {
  const id = useId();
  const [text, setText] = useState(initial);
  const trimmed = text.trim();

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        if (trimmed !== "") {
          onSubmit(trimmed);
        }
      }}
    >
      <label htmlFor={id} className="sr-only">
        Comment
      </label>
      <textarea
        id={id}
        value={text}
        onChange={(event) => setText(event.target.value)}
        maxLength={COMMENT_MAX_LENGTH}
        rows={3}
        placeholder="Add a comment"
        className={textareaClass}
      />
      <div className="mt-2 flex flex-wrap items-center gap-3">
        <FormError error={error} />
        <span className="ml-auto text-xs text-haze">
          {text.length} / {COMMENT_MAX_LENGTH}
        </span>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="rounded-sm px-3 py-1.5 text-sm font-medium text-haze ring-1 ring-white/15 transition hover:text-screen"
          >
            Cancel
          </button>
        )}
        <button
          type="submit"
          disabled={pending || trimmed === ""}
          className="rounded-sm bg-projector px-3 py-1.5 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-50"
        >
          {pending ? "Saving…" : submitLabel}
        </button>
      </div>
    </form>
  );
}

function CommentItem({ comment, reviewAuthorId }: { comment: ReviewComment; reviewAuthorId: number }) {
  const { user } = useAuth();
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const update = useUpdateComment(comment.reviewId);
  const remove = useDeleteComment(comment.reviewId);
  const own = user?.id === comment.userId;
  const canDelete = own || user?.id === reviewAuthorId;
  const created = new Date(comment.createdAt);

  return (
    <li className="py-5">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
        <UserLink
          user={{ id: comment.userId, userName: comment.username, profileImageUrl: comment.profileImageUrl }}
          avatarClassName="size-7 text-sm"
        />
        <time dateTime={comment.createdAt} title={fullFormat.format(created)} className="text-xs text-haze">
          {timeAgo(created)}
        </time>
        {comment.updatedAt && <span className="text-xs text-haze">(edited)</span>}
        {!editing && (own || canDelete) && (
          <span className="ml-auto flex items-center gap-3 text-xs">
            {confirming ? (
              <>
                <span className="text-haze">Delete this comment?</span>
                <button
                  type="button"
                  onClick={() => remove.mutate(comment.id)}
                  disabled={remove.isPending}
                  className="font-medium text-alarm underline underline-offset-4 disabled:opacity-50"
                >
                  {remove.isPending ? "Deleting…" : "Delete"}
                </button>
                <button
                  type="button"
                  onClick={() => setConfirming(false)}
                  className="text-haze underline underline-offset-4 hover:text-screen"
                >
                  Keep
                </button>
              </>
            ) : (
              <>
                {own && (
                  <button
                    type="button"
                    onClick={() => setEditing(true)}
                    className="text-haze underline underline-offset-4 hover:text-screen"
                  >
                    Edit
                  </button>
                )}
                {canDelete && (
                  <button
                    type="button"
                    onClick={() => setConfirming(true)}
                    className="text-haze underline underline-offset-4 hover:text-alarm"
                  >
                    Delete
                  </button>
                )}
              </>
            )}
          </span>
        )}
      </div>

      {editing ? (
        <CommentForm
          initial={comment.text}
          submitLabel="Save"
          pending={update.isPending}
          error={update.error}
          onSubmit={(text) => update.mutate({ id: comment.id, text }, { onSuccess: () => setEditing(false) })}
          onCancel={() => setEditing(false)}
        />
      ) : (
        <p className="mt-2 whitespace-pre-line break-words">{comment.text}</p>
      )}
      {remove.isError && (
        <p role="alert" className="mt-1 text-sm text-alarm">
          {remove.error.message}
        </p>
      )}
    </li>
  );
}

// Flat comment thread under a review, oldest first.
export function ReviewComments({ review }: { review: Review }) {
  const { user } = useAuth();
  const comments = useReviewComments(review.id);
  const add = useAddComment(review.id);
  // Remounting the form after a post clears it.
  const [formKey, setFormKey] = useState(0);
  const sectionRef = useRef<HTMLElement>(null);
  const { hash } = useLocation();
  const loaded = comments.isSuccess;

  // Links to "#comments" land here once the thread has loaded and the page has its full height.
  useEffect(() => {
    if (loaded && hash === "#comments") {
      sectionRef.current?.scrollIntoView();
    }
  }, [loaded, hash]);

  return (
    <section ref={sectionRef} id="comments" aria-labelledby="comments-title" className="mt-12 scroll-mt-24">
      <h2 id="comments-title" className="marquee text-3xl">
        Comments
        {comments.data && comments.data.length > 0 && (
          <span className="ml-2 text-xl text-haze">{comments.data.length}</span>
        )}
      </h2>

      {comments.isPending ? (
        <Loading label="Loading comments" />
      ) : comments.isError ? (
        <ErrorMessage error={comments.error} retry={() => comments.refetch()} />
      ) : comments.data.length === 0 ? (
        <p className="py-4 text-haze">No comments yet.</p>
      ) : (
        <ol className="mt-2 divide-y divide-white/5">
          {comments.data.map((comment) => (
            <CommentItem key={comment.id} comment={comment} reviewAuthorId={review.userId} />
          ))}
        </ol>
      )}

      <div className="mt-4 border-t border-white/5 pt-6">
        {user ? (
          <CommentForm
            key={formKey}
            submitLabel="Post comment"
            pending={add.isPending}
            error={add.error}
            onSubmit={(text) => add.mutate(text, { onSuccess: () => setFormKey((key) => key + 1) })}
          />
        ) : (
          <Link to="/login" className="text-sm font-medium text-projector underline underline-offset-4">
            Log in to comment
          </Link>
        )}
      </div>
    </section>
  );
}
