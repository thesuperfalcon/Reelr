import { useEffect, useRef, useState, type ChangeEvent, type FormEvent, type ReactNode } from "react";
import { Link, useBeforeUnload, useBlocker } from "react-router";
import { useAuth } from "../auth/auth-context";
import { Field, FormError } from "../components/Form";
import { ErrorMessage, Loading } from "../components/Status";
import { UserAvatar } from "../components/UserAvatar";
import { downloadDataExport } from "../lib/account";
import { useBlockedUsers, useToggleBlock } from "../lib/moderation";
import { prepareAvatar } from "../lib/avatar";
import {
  useChangePassword,
  useDeleteAccount,
  useLogoutEverywhere,
  useRemoveAvatar,
  useSettings,
  useUpdateSettings,
  useUploadAvatar,
} from "../lib/queries";
import type { DiaryVisibility, UserSettings, UserSettingsUpdate, UserSummary, WatchlistVisibility } from "../lib/types";

const buttonClass =
  "rounded-sm bg-projector px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-60";
const quietButtonClass =
  "rounded-sm px-4 py-2 text-sm font-medium text-screen ring-1 ring-white/15 transition hover:bg-row disabled:opacity-50";

// A new picture waits here until Save; "remove" likewise.
type PictureChange = { kind: "none" } | { kind: "upload"; image: Blob; preview: string } | { kind: "remove" };

interface Draft {
  userName: string;
  watchlistVisibility: WatchlistVisibility;
  diaryVisibility: DiaryVisibility;
  showFriendReviews: boolean;
  showOwnActivity: boolean;
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

function draftFrom(settings: UserSettings): Draft {
  return {
    userName: settings.userName,
    watchlistVisibility: settings.watchlistVisibility,
    diaryVisibility: settings.diaryVisibility,
    showFriendReviews: settings.showFriendReviews,
    showOwnActivity: settings.showOwnActivity,
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  };
}

// The settings fields that differ from what is saved, ready to send as a PATCH.
function changedSettings(draft: Draft, settings: UserSettings): UserSettingsUpdate {
  const update: UserSettingsUpdate = {};
  if (draft.userName.trim() !== settings.userName) update.userName = draft.userName.trim();
  if (draft.watchlistVisibility !== settings.watchlistVisibility) update.watchlistVisibility = draft.watchlistVisibility;
  if (draft.diaryVisibility !== settings.diaryVisibility) update.diaryVisibility = draft.diaryVisibility;
  if (draft.showFriendReviews !== settings.showFriendReviews) update.showFriendReviews = draft.showFriendReviews;
  if (draft.showOwnActivity !== settings.showOwnActivity) update.showOwnActivity = draft.showOwnActivity;
  return update;
}

function Section({ id, title, description, children }: { id: string; title: string; description?: string; children: ReactNode }) {
  return (
    <section aria-labelledby={id} className="grid gap-4 border-t border-white/5 py-10 md:grid-cols-[16rem_1fr] md:gap-10">
      <div>
        <h2 id={id} className="marquee text-2xl">
          {title}
        </h2>
        {description && <p className="mt-2 text-sm text-haze">{description}</p>}
      </div>
      <div className="min-w-0 max-w-md">{children}</div>
    </section>
  );
}

const diaryVisibilityOptions: { value: DiaryVisibility; label: string; description: string }[] = [
  { value: "Public", label: "Everyone", description: "Your diary, and what you log in the Community feed, are open to anyone." },
  { value: "Followers", label: "People who follow you", description: "Followers see your diary and your logs in their feed. Reviews stay public." },
  { value: "Private", label: "Only you", description: "Nobody else sees your diary or your logs. Reviews you write stay public." },
];

const visibilityOptions: { value: WatchlistVisibility; label: string; description: string }[] = [
  { value: "Public", label: "Everyone", description: "Anyone can see your watchlist, also people who are not logged in." },
  { value: "Followers", label: "People who follow you", description: "Followers see your watchlist and what you add to it in their feed." },
  { value: "Private", label: "Only you", description: "Nobody else sees your watchlist or what you add to it." },
];

function SettingsForm({ settings }: { settings: UserSettings }) {
  const update = useUpdateSettings();
  const changePassword = useChangePassword();
  const upload = useUploadAvatar();
  const remove = useRemoveAvatar();

  const fileInput = useRef<HTMLInputElement>(null);
  const [draft, setDraft] = useState(() => draftFrom(settings));
  const [picture, setPicture] = useState<PictureChange>({ kind: "none" });
  const [pictureError, setPictureError] = useState<unknown>(null);
  const [saveError, setSaveError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const settingsChanges = changedSettings(draft, settings);
  const passwordTouched = draft.currentPassword !== "" || draft.newPassword !== "" || draft.confirmPassword !== "";
  const dirty = Object.keys(settingsChanges).length > 0 || passwordTouched || picture.kind !== "none";

  // Previews are object URLs; release each one when it is replaced or the page is left.
  useEffect(() => {
    return () => {
      if (picture.kind === "upload") URL.revokeObjectURL(picture.preview);
    };
  }, [picture]);

  // Warn before losing unsaved changes, both on reload or tab close and on links inside the app.
  useBeforeUnload((event) => {
    if (dirty && !saving) event.preventDefault();
  });
  const blocker = useBlocker(({ currentLocation, nextLocation }) => dirty && !saving && currentLocation.pathname !== nextLocation.pathname);

  function edit<K extends keyof Draft>(key: K, value: Draft[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
    setSaved(false);
    setSaveError(null);
  }

  async function choosePicture(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;

    setPictureError(null);
    setSaved(false);
    try {
      const image = await prepareAvatar(file);
      setPicture({ kind: "upload", image, preview: URL.createObjectURL(image) });
    } catch (error) {
      setPictureError(error);
    }
  }

  function discard() {
    setDraft(draftFrom(settings));
    setPicture({ kind: "none" });
    setPictureError(null);
    setSaveError(null);
  }

  // Each part is saved in turn and cleared once it succeeds, so after a failure Save retries only what is left.
  async function save(event: FormEvent) {
    event.preventDefault();
    setSaveError(null);

    if (passwordTouched) {
      if (!draft.currentPassword || !draft.newPassword) {
        setSaveError(new Error("Fill in your current and new password, or clear the password fields."));
        return;
      }
      if (draft.newPassword !== draft.confirmPassword) {
        setSaveError(new Error("The new passwords do not match."));
        return;
      }
    }

    setSaving(true);
    try {
      if (picture.kind === "upload") {
        await upload.mutateAsync(picture.image);
        setPicture({ kind: "none" });
      } else if (picture.kind === "remove") {
        await remove.mutateAsync();
        setPicture({ kind: "none" });
      }

      if (Object.keys(settingsChanges).length > 0) {
        await update.mutateAsync(settingsChanges);
      }

      if (passwordTouched) {
        await changePassword.mutateAsync({ currentPassword: draft.currentPassword, newPassword: draft.newPassword });
        setDraft((current) => ({ ...current, currentPassword: "", newPassword: "", confirmPassword: "" }));
      }

      setSaved(true);
    } catch (error) {
      setSaveError(error);
    } finally {
      setSaving(false);
    }
  }

  const shownPicture = picture.kind === "upload" ? picture.preview : picture.kind === "remove" ? null : settings.profileImageUrl;

  return (
    <form onSubmit={save}>
      <Section id="settings-picture" title="Profile picture" description="Shown next to your name everywhere on Reelr.">
        <div className="flex flex-wrap items-center gap-6">
          <UserAvatar userName={draft.userName || settings.userName} imageUrl={shownPicture} className="size-24 text-5xl" />
          <div className="flex flex-wrap gap-3">
            <input
              ref={fileInput}
              id="avatar-file"
              type="file"
              accept="image/jpeg,image/png,image/webp,image/*"
              onChange={choosePicture}
              className="sr-only"
              tabIndex={-1}
            />
            <button type="button" onClick={() => fileInput.current?.click()} disabled={saving} className={quietButtonClass}>
              {shownPicture ? "Choose another picture" : "Choose picture"}
            </button>
            {shownPicture && (
              <button type="button" onClick={() => setPicture({ kind: "remove" })} disabled={saving} className={quietButtonClass}>
                Remove
              </button>
            )}
            {picture.kind !== "none" && (
              <button
                type="button"
                onClick={() => setPicture({ kind: "none" })}
                disabled={saving}
                className="text-sm font-medium text-haze underline underline-offset-4 hover:text-screen"
              >
                Keep current
              </button>
            )}
          </div>
        </div>
        <p className="mt-3 text-xs text-haze">
          JPEG, PNG or WebP. The picture is cropped to a square.
          {picture.kind === "upload" && " The new picture is saved when you press Save."}
          {picture.kind === "remove" && " The picture is removed when you press Save."}
        </p>
        <div className="mt-3">
          <FormError error={pictureError} />
        </div>
      </Section>

      <Section id="settings-account" title="Account">
        <div className="space-y-5">
          <Field
            id="settings-username"
            label="Username"
            autoComplete="username"
            minLength={3}
            maxLength={50}
            value={draft.userName}
            onChange={(event) => edit("userName", event.target.value)}
          />
          <div>
            <p className="text-sm font-medium">Email</p>
            <p className="mt-1.5 text-haze">{settings.email}</p>
          </div>
        </div>
      </Section>

      <Section id="settings-password" title="Password" description="Leave these empty to keep your current password.">
        <div className="space-y-5">
          <Field
            id="current-password"
            label="Current password"
            type="password"
            autoComplete="current-password"
            required={passwordTouched}
            value={draft.currentPassword}
            onChange={(event) => edit("currentPassword", event.target.value)}
          />
          <Field
            id="new-password"
            label="New password"
            type="password"
            autoComplete="new-password"
            minLength={8}
            required={passwordTouched}
            hint="At least 8 characters, with an upper and lower case letter, a digit and a symbol."
            value={draft.newPassword}
            onChange={(event) => edit("newPassword", event.target.value)}
          />
          <Field
            id="confirm-password"
            label="Repeat new password"
            type="password"
            autoComplete="new-password"
            required={passwordTouched}
            value={draft.confirmPassword}
            onChange={(event) => edit("confirmPassword", event.target.value)}
          />
        </div>
      </Section>

      <Section id="settings-privacy" title="Privacy">
        <fieldset className="mb-8">
          <legend className="text-sm font-medium">Who can see your diary</legend>
          <div className="mt-3 space-y-3">
            {diaryVisibilityOptions.map((option) => (
              <label key={option.value} className="flex items-start gap-3 text-sm">
                <input
                  type="radio"
                  name="diary-visibility"
                  value={option.value}
                  checked={draft.diaryVisibility === option.value}
                  onChange={() => edit("diaryVisibility", option.value)}
                  className="mt-0.5 size-4 accent-projector"
                />
                <span>
                  <span className="font-medium">{option.label}</span>
                  <span className="block text-haze">{option.description}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend className="text-sm font-medium">Who can see your watchlist</legend>
          <div className="mt-3 space-y-3">
            {visibilityOptions.map((option) => (
              <label key={option.value} className="flex items-start gap-3 text-sm">
                <input
                  type="radio"
                  name="watchlist-visibility"
                  value={option.value}
                  checked={draft.watchlistVisibility === option.value}
                  onChange={() => edit("watchlistVisibility", option.value)}
                  className="mt-0.5 size-4 accent-projector"
                />
                <span>
                  <span className="font-medium">{option.label}</span>
                  <span className="block text-haze">{option.description}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>
      </Section>

      <Section id="settings-start" title="Start page">
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            checked={draft.showFriendReviews}
            onChange={(event) => edit("showFriendReviews", event.target.checked)}
            className="mt-0.5 size-4 accent-projector"
          />
          <span>
            <span className="font-medium">Show reviews from friends</span>
            <span className="block text-haze">The newest reviews from people you follow, below "New from friends".</span>
          </span>
        </label>
      </Section>

      <Section id="settings-activity" title="Activity">
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            checked={draft.showOwnActivity}
            onChange={(event) => edit("showOwnActivity", event.target.checked)}
            className="mt-0.5 size-4 accent-projector"
          />
          <span>
            <span className="font-medium">Show my activity in Following</span>
            <span className="block text-haze">
              Your own logs, reviews, lists and watchlist additions appear alongside the people you follow. Only you see
              them there.
            </span>
          </span>
        </label>
      </Section>

      {/* Stays in view while scrolling, so the unsaved state is always visible. */}
      <div className="sticky bottom-0 -mx-4 border-t border-white/10 bg-salon/95 px-4 py-4 backdrop-blur sm:-mx-6 sm:px-6">
        {blocker.state === "blocked" ? (
          <div role="alertdialog" aria-label="Unsaved changes" className="flex flex-wrap items-center gap-x-6 gap-y-3">
            <p className="text-sm">You have unsaved changes. Leave without saving?</p>
            <div className="flex gap-3">
              <button type="button" onClick={() => blocker.reset()} className={buttonClass}>
                Stay
              </button>
              <button type="button" onClick={() => blocker.proceed()} className={quietButtonClass}>
                Leave
              </button>
            </div>
          </div>
        ) : (
          <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
            <div className="flex gap-3">
              <button type="submit" disabled={!dirty || saving} className={buttonClass}>
                {saving ? "Saving…" : "Save changes"}
              </button>
              {dirty && (
                <button type="button" onClick={discard} disabled={saving} className={quietButtonClass}>
                  Discard
                </button>
              )}
            </div>
            <div role="status" className="min-w-0 text-sm">
              {saveError ? (
                <FormError error={saveError} />
              ) : dirty ? (
                <span className="text-projector">You have unsaved changes.</span>
              ) : saved ? (
                <span className="text-haze">Saved.</span>
              ) : null}
            </div>
          </div>
        )}
      </div>
    </form>
  );
}

function BlockedSection() {
  const blocked = useBlockedUsers();

  return (
    <Section
      id="settings-blocked"
      title="Blocked members"
      description="Blocked members cannot follow you, comment on or like your reviews, and you do not see their reviews, comments or community activity."
    >
      {blocked.isPending ? (
        <Loading label="Loading blocked members" />
      ) : blocked.isError ? (
        <ErrorMessage error={blocked.error} retry={() => blocked.refetch()} />
      ) : blocked.data.length === 0 ? (
        <p className="text-sm text-haze">You have not blocked anyone.</p>
      ) : (
        <ul className="space-y-3">
          {blocked.data.map((member) => (
            <BlockedMember key={member.id} member={member} />
          ))}
        </ul>
      )}
    </Section>
  );
}

function BlockedMember({ member }: { member: UserSummary }) {
  const toggle = useToggleBlock(member.id);

  return (
    <li className="flex items-center justify-between gap-4 text-sm">
      <Link to={`/user/${member.id}`} className="flex min-w-0 items-center gap-3 hover:text-projector">
        <UserAvatar userName={member.userName} imageUrl={member.profileImageUrl} className="size-8 text-sm" />
        <span className="truncate">{member.userName}</span>
      </Link>
      <button type="button" onClick={() => toggle.mutate(true)} disabled={toggle.isPending} className={quietButtonClass}>
        {toggle.isPending ? "Unblocking…" : "Unblock"}
      </button>
    </li>
  );
}

function DataExportSection() {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<unknown>(null);

  async function download() {
    setPending(true);
    setError(null);
    try {
      await downloadDataExport();
    } catch (err) {
      setError(err);
    } finally {
      setPending(false);
    }
  }

  return (
    <Section
      id="settings-data"
      title="Your data"
      description="A file with everything Reelr stores about you: account, diary, ratings, reviews, comments, lists, watchlist and follows."
    >
      <FormError error={error} />
      <button type="button" onClick={download} disabled={pending} className={quietButtonClass}>
        {pending ? "Preparing…" : "Download my data"}
      </button>
    </Section>
  );
}

// Signs out every device, for example after using a shared or lost one.
function LogoutEverywhereSection() {
  const { logout } = useAuth();
  const logoutEverywhere = useLogoutEverywhere();

  async function signOutEverywhere() {
    try {
      await logoutEverywhere.mutateAsync();
      logout();
      // A full load, for the same reason as after deleting the account below.
      window.location.replace("/login");
    } catch {
      // The error is shown from logoutEverywhere.error.
    }
  }

  return (
    <Section
      id="settings-sessions"
      title="Devices"
      description="Logs you out on every phone, computer and browser where you are logged in, this one included."
    >
      <FormError error={logoutEverywhere.error} />
      <button
        type="button"
        onClick={signOutEverywhere}
        disabled={logoutEverywhere.isPending}
        className={quietButtonClass}
      >
        {logoutEverywhere.isPending ? "Logging out…" : "Log out everywhere"}
      </button>
    </Section>
  );
}

// Its own form outside SettingsForm, so Save never touches it and deleting needs a separate, explicit step.
function DeleteAccountSection({ userName }: { userName: string }) {
  const { user, logout } = useAuth();
  const deleteAccount = useDeleteAccount();
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState("");

  function cancel() {
    setOpen(false);
    setPassword("");
    deleteAccount.reset();
  }

  async function confirm(event: FormEvent) {
    event.preventDefault();
    if (!user) return;

    try {
      await deleteAccount.mutateAsync({ userId: user.id, password });
      logout();
      // A full load of the start page. A router navigation races with RequireAuth, which sees no user
      // on /settings and sends the visitor to /login instead.
      window.location.replace("/");
    } catch {
      // The error is shown from deleteAccount.error.
    }
  }

  return (
    <Section
      id="settings-delete"
      title="Delete account"
      description="Removes your profile, diary, ratings, reviews, lists, watchlist, follows, likes and comments. This cannot be undone."
    >
      {open ? (
        <form onSubmit={confirm} className="space-y-5">
          <p className="text-sm">
            Enter your password to delete <span className="font-semibold">{userName}</span> for good.
          </p>
          <Field
            id="delete-password"
            label="Password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            hint="Wrong passwords count towards the login lockout."
          />
          <FormError error={deleteAccount.error} />
          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              disabled={!password || deleteAccount.isPending}
              className="rounded-sm bg-alarm px-4 py-2 text-sm font-semibold text-salon transition hover:brightness-110 disabled:opacity-60"
            >
              {deleteAccount.isPending ? "Deleting…" : "Delete my account"}
            </button>
            <button type="button" onClick={cancel} disabled={deleteAccount.isPending} className={quietButtonClass}>
              Cancel
            </button>
          </div>
        </form>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="rounded-sm px-4 py-2 text-sm font-medium text-alarm ring-1 ring-alarm/40 transition hover:bg-alarm/10"
        >
          Delete account…
        </button>
      )}
    </Section>
  );
}

export function SettingsPage() {
  const settings = useSettings();

  return (
    <div className="mx-auto max-w-6xl px-4 pt-10 sm:px-6 sm:pt-14">
      <p className="text-sm text-projector">
        <Link to="/profile" className="hover:underline">
          Your profile
        </Link>
      </p>
      <h1 className="marquee mt-3 text-6xl sm:text-7xl">Settings</h1>

      <div className="mt-10">
        {settings.isPending ? (
          <Loading label="Loading your settings" />
        ) : settings.isError ? (
          <ErrorMessage error={settings.error} retry={() => settings.refetch()} />
        ) : (
          <>
            <SettingsForm settings={settings.data} />
            <BlockedSection />
            <DataExportSection />
            <LogoutEverywhereSection />
            <DeleteAccountSection userName={settings.data.userName} />
          </>
        )}
      </div>
    </div>
  );
}
