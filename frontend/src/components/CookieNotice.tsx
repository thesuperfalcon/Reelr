import { useState } from "react";
import { Link } from "react-router";

const STORAGE_KEY = "reelr.cookieNotice";

function seen(): boolean {
  try {
    return localStorage.getItem(STORAGE_KEY) !== null;
  } catch {
    // Storage can be blocked (private mode). The note then shows on each visit, which does no harm.
    return false;
  }
}

// Says once what Reelr keeps in the browser. Everything it keeps is needed for the site to work, so this is a
// note rather than a consent banner; adding tracking would need real consent first (see the privacy page).
// It sits under the header instead of over the page, so it never hides content or the control in focus.
export function CookieNotice() {
  const [open, setOpen] = useState(() => !seen());

  if (!open) {
    return null;
  }

  function dismiss() {
    try {
      localStorage.setItem(STORAGE_KEY, "1");
    } catch {
      // See seen().
    }
    setOpen(false);
    // The button disappears, so focus goes to the page instead of being lost (WCAG 2.4.3).
    document.getElementById("main")?.focus({ preventScroll: true });
  }

  return (
    <section aria-label="Cookies" className="border-b border-rule/10 bg-row">
      <div className="page flex flex-wrap items-center gap-x-4 gap-y-2 py-2.5 text-sm">
        <p className="text-screen">
          Reelr uses one cookie, only to keep you logged in, and no tracking.{" "}
          <Link to="/privacy" className="link">
            How Reelr handles your data
          </Link>
        </p>
        <button type="button" onClick={dismiss} className="btn btn-sm btn-secondary sm:ml-auto">
          OK
        </button>
      </div>
    </section>
  );
}
