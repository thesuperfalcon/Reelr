import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { FormError } from "../components/Form";
import { EmptyState, ErrorMessage, Loading } from "../components/Status";
import { UserAvatar } from "../components/UserAvatar";
import {
  INVITE_NOTE_MAX_LENGTH,
  useCreateInvite,
  useInvites,
  useRevokeInvite,
  type CreatedInvite,
  type Invite,
} from "../lib/invites";
import { usePageViewStats } from "../lib/analytics";
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
    <li className="border-t border-rule/5 py-6">
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
            <Link to={link} className="link mt-3 inline-block text-sm">
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
            <li key={index} className="border-l-2 border-rule/10 pl-3 text-haze">
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
            className="btn btn-danger"
          >
            Remove {typeLabel[item.targetType].toLowerCase()}
          </button>
        )}
        <button
          type="button"
          onClick={() => resolve.mutate({ targetType: item.targetType, targetId: item.targetId, outcome: "Dismissed" })}
          disabled={resolve.isPending}
          className="btn btn-secondary"
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
  if (reports.data.length === 0) return <EmptyState className="mt-6">No open reports. Reports from members show up here.</EmptyState>;

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
          className="btn btn-sm btn-secondary"
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
      <h2 id="invites-heading" className="heading-section">
        Invites
      </h2>
      <p className="mt-2 text-sm text-haze">Registration is invite only. Each code creates one account.</p>

      <form onSubmit={submit} className="mt-4 flex flex-wrap items-end gap-3">
        <div className="min-w-48 flex-1">
          <label htmlFor="invite-note" className="label">
            Who is it for? <span className="text-haze">(optional)</span>
          </label>
          <input
            id="invite-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={INVITE_NOTE_MAX_LENGTH}
            className="input mt-1.5"
          />
        </div>
        <button
          type="submit"
          disabled={create.isPending}
          className="btn btn-primary"
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
        <EmptyState className="mt-6">No invites yet. Create one above to let someone join.</EmptyState>
      ) : (
        <ul className="mt-6">
          {invites.data.map((invite) => (
            <li key={invite.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-rule/5 py-3 text-sm">
              <span className="font-medium">{invite.note ?? `Invite ${invite.id}`}</span>
              <span className="text-haze">{inviteStatus(invite)}</span>
              {!invite.usedAt && !invite.revokedAt && (
                <button
                  type="button"
                  onClick={() => revoke.mutate(invite.id)}
                  disabled={revoke.isPending}
                  className="btn btn-sm btn-secondary ml-auto"
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

// How often each kind of page was opened in the last 30 days (see lib/analytics.ts).
function PageViews() {
  const stats = usePageViewStats();

  return (
    <section aria-labelledby="page-views-heading" className="mt-12">
      <h2 id="page-views-heading" className="heading-section">
        Page views
      </h2>
      {stats.isPending ? (
        <Loading />
      ) : stats.isError ? (
        <ErrorMessage error={stats.error} retry={() => stats.refetch()} />
      ) : (
        <>
          <p className="mt-2 text-sm text-haze">
            {stats.data.totalViews.toLocaleString("en")} in the last {stats.data.days} days, counted per kind of page
            without cookies or anything that identifies the visitor.
          </p>
          {stats.data.perRoute.length > 0 && (
            <table className="mt-5 w-full text-sm">
              <caption className="sr-only">Page views per kind of page in the last {stats.data.days} days</caption>
              <thead>
                <tr className="border-b border-rule/10 text-left text-haze">
                  <th scope="col" className="py-2 font-medium">
                    Page
                  </th>
                  <th scope="col" className="py-2 text-right font-medium">
                    Views
                  </th>
                </tr>
              </thead>
              <tbody>
                {stats.data.perRoute.map((row) => (
                  <tr key={row.route} className="border-b border-rule/5">
                    <td className="py-2 font-mono">{row.route}</td>
                    <td className="py-2 text-right tabular-nums">{row.views.toLocaleString("en")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </section>
  );
}

// Invites, reported reviews, comments and lists (the most reported first), and page views. Only admins get past
// the check.
export function AdminPage() {
  usePageTitle("Admin");
  const settings = useSettings();

  if (settings.isPending) return <Loading />;
  if (!settings.data?.isAdmin) return <NotFoundPage />;

  return (
    <div className="page py-10 *:max-w-3xl sm:py-14">
      <h1 className="heading-page">Admin</h1>
      <Invites />
      <section aria-labelledby="reports-heading" className="mt-12">
        <h2 id="reports-heading" className="heading-section">
          Reports
        </h2>
        <p className="mt-2 text-sm text-haze">
          Removing deletes the item for everyone. Dismissing keeps it and closes the reports.
        </p>
        <OpenReports />
      </section>
      <PageViews />
    </div>
  );
}
