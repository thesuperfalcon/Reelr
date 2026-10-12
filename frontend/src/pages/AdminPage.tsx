import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { FormError } from "../components/Form";
import { ErrorMessage, Loading } from "../components/Status";
import { UserAvatar } from "../components/UserAvatar";
import {
  INVITE_NOTE_MAX_LENGTH,
  useCreateInvite,
  useInvites,
  useRevokeInvite,
  type CreatedInvite,
  type Invite,
} from "../lib/invites";
import { reportReasons, useOpenReports, useResolveReports, type ReportedItem } from "../lib/moderation";
import { useSettings } from "../lib/queries";
import { NotFoundPage } from "./NotFoundPage";
import { usePageTitle } from "../lib/title";

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

const reasonLabel = (reason: string) => reportReasons.find((r) => r.value === reason)?.label ?? reason;

const typeLabel = { Review: "Review", Comment: "Comment", List: "List" } as const;

function itemLink(item: ReportedItem) {
  if (item.listId) return `/list/${item.listId}`;
  if (item.reviewId) return `/review/${item.reviewId}`;
  return null;
}

function ReportedItemCard({ item }: { item: ReportedItem }) {
  const resolve = useResolveReports();
  const link = itemLink(item);

  return (
    <li className="border-t border-white/5 py-6">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
        <span className="rounded-sm bg-row px-2 py-0.5 font-medium">{typeLabel[item.targetType]}</span>
        <span className="text-alarm">
          {item.reportCount} {item.reportCount === 1 ? "report" : "reports"}
        </span>
        <span className="text-haze">
          First <time dateTime={item.firstReportedAt}>{dateFormat.format(new Date(item.firstReportedAt))}</time>
        </span>
      </div>

      <p className="mt-2 text-sm text-haze">{item.reasons.map(reasonLabel).join(" · ")}</p>

      {item.exists ? (
        <div className="mt-4 rounded-sm bg-row p-4">
          {item.author && (
            <Link to={`/user/${item.author.id}`} className="flex items-center gap-2 text-sm hover:text-projector">
              <UserAvatar userName={item.author.userName} imageUrl={item.author.profileImageUrl} className="size-6 text-xs" />
              {item.author.userName}
            </Link>
          )}
          {item.context && <p className="mt-2 text-xs text-haze">{item.context}</p>}
          {/* Shown as plain text, never as Markdown or HTML: reported content is the least trusted text there is. */}
          <p className="mt-3 whitespace-pre-wrap text-sm">{item.text}</p>
          {link && (
            <Link to={link} className="mt-3 inline-block text-sm text-projector underline underline-offset-4">
              Open
            </Link>
          )}
        </div>
      ) : (
        <p className="mt-4 text-sm text-haze">Already deleted. Dismiss the reports to clear them.</p>
      )}

      {item.notes.length > 0 && (
        <ul className="mt-4 space-y-1 text-sm">
          {item.notes.map((note, index) => (
            <li key={index} className="border-l-2 border-white/10 pl-3 text-haze">
              {note}
            </li>
          ))}
        </ul>
      )}

      <FormError error={resolve.error} />
      <div className="mt-4 flex flex-wrap gap-3">
        {item.exists && (
          <button
            type="button"
            onClick={() => resolve.mutate({ targetType: item.targetType, targetId: item.targetId, outcome: "Removed" })}
            disabled={resolve.isPending}
            className="rounded-sm bg-alarm px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-60"
          >
            Remove {typeLabel[item.targetType].toLowerCase()}
          </button>
        )}
        <button
          type="button"
          onClick={() => resolve.mutate({ targetType: item.targetType, targetId: item.targetId, outcome: "Dismissed" })}
          disabled={resolve.isPending}
          className="rounded-sm px-4 py-2 text-sm font-medium text-screen ring-1 ring-white/15 transition hover:bg-row disabled:opacity-60"
        >
          Dismiss
        </button>
      </div>
    </li>
  );
}

function OpenReports() {
  const reports = useOpenReports();

  if (reports.isPending) return <Loading label="Loading reports" />;
  if (reports.isError) return <ErrorMessage error={reports.error} retry={() => reports.refetch()} />;
  if (reports.data.length === 0) return <p className="mt-6 text-haze">No open reports.</p>;

  return (
    <ul className="mt-6">
      {reports.data.map((item) => (
        <ReportedItemCard key={`${item.targetType}-${item.targetId}`} item={item} />
      ))}
    </ul>
  );
}

function inviteStatus(invite: Invite) {
  if (invite.usedAt) {
    return `Used ${dateFormat.format(new Date(invite.usedAt))}${invite.usedBy ? ` by ${invite.usedBy}` : " (account deleted)"}`;
  }
  if (invite.revokedAt) return `Revoked ${dateFormat.format(new Date(invite.revokedAt))}`;
  return "Not used yet";
}

// The link is shown once, right after the invite is made: only a hash of the code is stored.
function NewInvite({ invite }: { invite: CreatedInvite }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    await navigator.clipboard.writeText(invite.link);
    setCopied(true);
  }

  return (
    <div className="mt-4 rounded-sm bg-row p-4 text-sm">
      <p className="font-medium">
        Invite {invite.note ? `for ${invite.note} ` : ""}ready. Send this link; it is shown only now.
      </p>
      <div className="mt-3 flex flex-wrap items-center gap-3">
        <code className="break-all rounded-sm bg-salon px-2 py-1 text-xs">{invite.link}</code>
        <button
          type="button"
          onClick={copy}
          className="rounded-sm px-3 py-1.5 text-sm font-medium ring-1 ring-white/15 transition hover:bg-row-raised"
        >
          Copy link
        </button>
        <span role="status" className="text-haze">
          {copied ? "Copied." : ""}
        </span>
      </div>
    </div>
  );
}

function Invites() {
  const invites = useInvites();
  const create = useCreateInvite();
  const revoke = useRevokeInvite();
  const [note, setNote] = useState("");

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    create.mutate(note.trim(), { onSuccess: () => setNote("") });
  }

  return (
    <section aria-labelledby="invites-heading" className="mt-10">
      <h2 id="invites-heading" className="text-2xl font-semibold">
        Invites
      </h2>
      <p className="mt-2 text-sm text-haze">Registration is invite only. Each code creates one account.</p>

      <form onSubmit={submit} className="mt-4 flex flex-wrap items-end gap-3">
        <div className="min-w-48 flex-1">
          <label htmlFor="invite-note" className="block text-sm font-medium">
            Who is it for? <span className="text-haze">(optional)</span>
          </label>
          <input
            id="invite-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={INVITE_NOTE_MAX_LENGTH}
            className="mt-1.5 w-full rounded-sm bg-row px-3 py-2 text-screen ring-1 ring-edge focus:bg-row-raised focus:outline-2 focus:outline-projector"
          />
        </div>
        <button
          type="submit"
          disabled={create.isPending}
          className="rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-60"
        >
          {create.isPending ? "Making invite…" : "Make invite"}
        </button>
      </form>
      <FormError error={create.error} />
      {create.data && <NewInvite key={create.data.id} invite={create.data} />}

      {invites.isPending ? (
        <Loading label="Loading invites" />
      ) : invites.isError ? (
        <ErrorMessage error={invites.error} retry={() => invites.refetch()} />
      ) : invites.data.length === 0 ? (
        <p className="mt-6 text-haze">No invites yet.</p>
      ) : (
        <ul className="mt-6">
          {invites.data.map((invite) => (
            <li key={invite.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-white/5 py-3 text-sm">
              <span className="font-medium">{invite.note ?? `Invite ${invite.id}`}</span>
              <span className="text-haze">{inviteStatus(invite)}</span>
              {!invite.usedAt && !invite.revokedAt && (
                <button
                  type="button"
                  onClick={() => revoke.mutate(invite.id)}
                  disabled={revoke.isPending}
                  className="ml-auto rounded-sm px-3 py-1 text-sm ring-1 ring-white/15 transition hover:bg-row disabled:opacity-60"
                >
                  Revoke<span className="sr-only"> {invite.note ?? `invite ${invite.id}`}</span>
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      <FormError error={revoke.error} />
    </section>
  );
}

// Invites, and reported reviews, comments and lists, the most reported first. Only admins get past the check.
export function AdminPage() {
  usePageTitle("Admin");
  const settings = useSettings();

  if (settings.isPending) return <Loading />;
  if (!settings.data?.isAdmin) return <NotFoundPage />;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6 sm:py-14">
      <h1 className="marquee text-6xl sm:text-7xl">Admin</h1>
      <Invites />
      <section aria-labelledby="reports-heading" className="mt-12">
        <h2 id="reports-heading" className="text-2xl font-semibold">
          Reports
        </h2>
        <p className="mt-2 text-sm text-haze">
          Removing deletes the item for everyone. Dismissing keeps it and closes the reports.
        </p>
        <OpenReports />
      </section>
    </div>
  );
}
