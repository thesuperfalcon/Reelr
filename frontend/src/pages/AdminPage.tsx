import { Link } from "react-router";
import { FormError } from "../components/Form";
import { ErrorMessage, Loading } from "../components/Status";
import { UserAvatar } from "../components/UserAvatar";
import { reportReasons, useOpenReports, useResolveReports, type ReportedItem } from "../lib/moderation";
import { useSettings } from "../lib/queries";
import { NotFoundPage } from "./NotFoundPage";

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

// Reported reviews, comments and lists, the most reported first. Only admins get past the check.
export function AdminPage() {
  const settings = useSettings();

  if (settings.isPending) return <Loading />;
  if (!settings.data?.isAdmin) return <NotFoundPage />;

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-6 sm:py-14">
      <h1 className="marquee text-6xl sm:text-7xl">Moderation</h1>
      <p className="mt-3 text-sm text-haze">
        Removing deletes the item for everyone. Dismissing keeps it and closes the reports.
      </p>
      <OpenReports />
    </div>
  );
}
