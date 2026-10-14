import { useEffect, useId, useRef, useState, type FocusEvent, type FormEvent } from "react";
import { Link, NavLink, Outlet, ScrollRestoration, useLocation, useNavigate, useNavigation, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { usePageViews } from "../lib/analytics";
import { useSettings } from "../lib/queries";
import { JoinBar } from "./JoinBar";
import { Loading } from "./Status";

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-4" aria-hidden="true">
      <circle cx="11" cy="11" r="6.5" />
      <path d="M16 16l4.5 4.5" />
    </svg>
  );
}

function SearchBox() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [query, setQuery] = useState(params.get("q") ?? "");

  function submit(event: FormEvent) {
    event.preventDefault();
    const trimmed = query.trim();
    if (trimmed) {
      navigate(`/search?q=${encodeURIComponent(trimmed)}`);
    }
  }

  return (
    <form role="search" onSubmit={submit} className="relative w-full sm:w-72">
      <label htmlFor="site-search" className="sr-only">
        Search films, people, studios and members
      </label>
      <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-haze">
        <SearchIcon />
      </span>
      <input
        id="site-search"
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder="Search films, people, members"
        className="input py-2 pl-9 text-sm"
      />
    </form>
  );
}

function UserIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="size-4" aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </svg>
  );
}

function ChevronIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={`size-4 transition-transform ${open ? "rotate-180" : ""}`}
      aria-hidden="true"
    >
      <path d="M6 9l6 6 6-6" />
    </svg>
  );
}

// Pages are loaded the first time they are opened. A thin bar shows while that takes a moment.
function PageLoadingBar() {
  const loading = useNavigation().state === "loading";

  return (
    <div
      aria-hidden="true"
      className={`fixed inset-x-0 top-0 z-50 h-0.5 bg-projector transition-opacity duration-300 ${
        loading ? "opacity-100 delay-150" : "opacity-0"
      }`}
    />
  );
}

// Tells the user when their sign-in ended while the page was open, so unsaved work is not lost without
// warning. Review text in the rating dialog is kept as a draft, so it is still there after logging in again.
function SessionBanner() {
  const { sessionNotice, dismissSessionNotice } = useAuth();
  const location = useLocation();

  const onLoginPage = location.pathname === "/login";

  if (!sessionNotice) {
    return null;
  }

  return (
    <div role="status" className="border-b border-projector/30 bg-row">
      <div className="page flex flex-wrap items-center gap-x-4 gap-y-2 py-2.5 text-sm">
        <p className="text-screen">
          Your login has ended. Log in again to keep going; unsaved review text is kept.
        </p>
        <div className="flex items-center gap-4 sm:ml-auto">
          {!onLoginPage && (
            <Link
              to="/login"
              state={{ from: location.pathname + location.search }}
              onClick={dismissSessionNotice}
              className="btn btn-sm btn-primary"
            >
              Log in again
            </Link>
          )}
          <button type="button" onClick={dismissSessionNotice} className="link-quiet">
            Dismiss
          </button>
        </div>
      </div>
    </div>
  );
}

// The current page's link is marked with an accent bar as well as brighter text, so colour is not the only cue.
const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `inline-flex h-10 items-center border-b-2 text-sm font-medium transition-colors hover:text-screen ${
    isActive ? "border-projector text-screen" : "border-transparent text-haze"
  }`;

const menuItemClass =
  "flex w-full items-center rounded-sm px-3 py-2.5 text-left text-sm text-screen transition-colors hover:bg-row-raised";

// Settings, admin and log out behind the account button, so the header fits on one line on a phone.
// A disclosure rather than an ARIA menu: Tab moves through the links, Escape or a click elsewhere closes it.
function AccountMenu({ isAdmin, onLogout }: { isAdmin: boolean; onLogout: () => void }) {
  const [open, setOpen] = useState(false);
  const { pathname } = useLocation();
  const [shownFor, setShownFor] = useState(pathname);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const panelId = useId();

  // Moving to another page closes it.
  if (shownFor !== pathname) {
    setShownFor(pathname);
    setOpen(false);
  }

  useEffect(() => {
    if (!open) {
      return;
    }

    function onPointerDown(event: PointerEvent) {
      if (!wrapperRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpen(false);
        buttonRef.current?.focus();
      }
    }

    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  // Tabbing out of the open menu closes it.
  function onBlur(event: FocusEvent<HTMLDivElement>) {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setOpen(false);
    }
  }

  return (
    <div ref={wrapperRef} onBlur={onBlur} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        aria-label="Account menu"
        onClick={() => setOpen(!open)}
        className="btn btn-icon btn-sm text-haze hover:bg-row hover:text-screen aria-expanded:bg-row aria-expanded:text-screen"
      >
        <ChevronIcon open={open} />
      </button>
      <div
        id={panelId}
        hidden={!open}
        className="surface absolute top-full right-0 z-40 mt-2 w-52 p-1.5 shadow-2xl ring-rule/10"
      >
        <ul>
          <li>
            <Link to="/settings" onClick={() => setOpen(false)} className={menuItemClass}>
              Settings
            </Link>
          </li>
          {isAdmin && (
            <li>
              <Link to="/admin" onClick={() => setOpen(false)} className={menuItemClass}>
                Admin
              </Link>
            </li>
          )}
          <li className="mt-1.5 border-t border-rule/10 pt-1.5">
            <button type="button" onClick={onLogout} className={`${menuItemClass} text-haze hover:text-screen`}>
              Log out
            </button>
          </li>
        </ul>
      </div>
    </div>
  );
}

// After a move to another page, focus goes to the main content, so screen readers start reading the new page
// there and keyboard users continue from it instead of from the link they used (WCAG 2.4.3). Changes within a
// page, such as another tab in its query string, keep focus where it is.
function useFocusOnNavigation() {
  const { pathname } = useLocation();
  const mainRef = useRef<HTMLElement>(null);
  const firstPage = useRef(true);

  useEffect(() => {
    if (firstPage.current) {
      firstPage.current = false;
      return;
    }
    mainRef.current?.focus({ preventScroll: true });
  }, [pathname]);

  return mainRef;
}

export function Layout() {
  const { user, restoring, logout } = useAuth();
  const mainRef = useFocusOnNavigation();
  usePageViews();
  // Shares the cached settings with the settings page, so a new picture shows here as soon as it is saved.
  const settings = useSettings(user !== null);
  const profileImageUrl = user ? settings.data?.profileImageUrl : null;

  return (
    <div className="flex min-h-dvh flex-col">
      <ScrollRestoration />
      {/* The first thing a keyboard user reaches, to jump past the header (WCAG 2.4.1). */}
      <a
        href="#main"
        className="sr-only rounded-sm bg-projector px-4 py-2 font-semibold text-salon focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50"
      >
        Skip to content
      </a>
      <PageLoadingBar />
      <header className="border-b border-rule/10">
        <div className="page flex flex-wrap items-center gap-x-4 gap-y-2 py-3 sm:gap-x-8 sm:py-4">
          <Link to="/" className="marquee rounded-sm text-3xl text-projector">
            Reelr
          </Link>

          <nav className="flex items-center gap-4 sm:gap-6" aria-label="Main">
            <NavLink to="/" end className={navLinkClass}>
              Films
            </NavLink>
            <NavLink to="/activity" className={navLinkClass}>
              Activity
            </NavLink>
          </nav>

          <div className="order-last w-full pb-1 sm:order-none sm:ml-auto sm:w-auto sm:pb-0">
            <SearchBox />
          </div>

          <div className="ml-auto flex items-center gap-1 sm:ml-0">
            {/* Empty until the sign-in is restored, so a signed-in visitor never sees "Log in" flash by. */}
            {restoring ? null : user ? (
              <>
                <NavLink
                  to="/profile"
                  aria-label={`Profile of ${user.username}`}
                  title="Your profile"
                  className={({ isActive }) =>
                    `flex h-10 items-center gap-2 rounded-full pr-1 text-sm font-medium transition-colors hover:text-screen ${
                      isActive ? "text-screen" : "text-haze"
                    }`
                  }
                >
                  {({ isActive }) => (
                    <>
                      {profileImageUrl ? (
                        <img
                          src={profileImageUrl}
                          alt=""
                          className={`size-8 rounded-full object-cover ring-2 transition ${
                            isActive ? "ring-projector" : "ring-transparent hover:ring-rule/20"
                          }`}
                        />
                      ) : (
                        <span
                          className={`flex size-8 items-center justify-center rounded-full transition-colors ${
                            isActive ? "bg-projector text-salon" : "bg-row text-screen hover:bg-row-raised"
                          }`}
                        >
                          <UserIcon />
                        </span>
                      )}
                      <span className="hidden max-w-32 truncate sm:inline">{user.username}</span>
                    </>
                  )}
                </NavLink>
                <AccountMenu isAdmin={settings.data?.isAdmin === true} onLogout={logout} />
              </>
            ) : (
              <div className="flex items-center gap-3 sm:gap-4">
                <NavLink to="/login" className={navLinkClass}>
                  Log in
                </NavLink>
                <Link to="/register" className="btn btn-sm btn-primary">
                  <span className="sm:hidden">Sign up</span>
                  <span className="hidden sm:inline">Create account</span>
                </Link>
              </div>
            )}
          </div>
        </div>
      </header>
      <SessionBanner />

      {/* Focused in code only (skip link, page changes), so it needs no visible focus ring. */}
      <main id="main" ref={mainRef} tabIndex={-1} className="flex-1 outline-none">
        {/* The header and footer show at once. The page waits for the restored sign-in, so a signed-in visitor
            is not sent to the login page first and nothing is fetched as a logged-out visitor. */}
        {restoring ? (
          <div className="page">
            <Loading label="Signing in" />
          </div>
        ) : (
          <Outlet />
        )}
      </main>

      <footer className="mt-10 border-t border-rule/10">
        <div className="page flex flex-col gap-4 py-8 text-xs text-haze sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="marquee text-xl text-screen/80" aria-hidden="true">
              Reelr
            </span>
            <nav aria-label="About Reelr" className="flex gap-4">
              <Link to="/privacy" className="rounded-xs py-1 hover:text-screen">
                Privacy
              </Link>
              <Link to="/terms" className="rounded-xs py-1 hover:text-screen">
                Terms
              </Link>
              <Link to="/accessibility" className="rounded-xs py-1 hover:text-screen">
                Accessibility
              </Link>
              <Link to="/contact" className="rounded-xs py-1 hover:text-screen">
                Contact
              </Link>
            </nav>
          </div>
          <p>Film data and images from TMDB. Reelr is not endorsed or certified by TMDB.</p>
        </div>
      </footer>
      <JoinBar />
    </div>
  );
}
