import { useState, type FormEvent } from "react";
import { Link, NavLink, Outlet, ScrollRestoration, useNavigate, useSearchParams } from "react-router";
import { useAuth } from "../auth/auth-context";

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
    <form role="search" onSubmit={submit} className="w-full sm:w-72">
      <label htmlFor="site-search" className="sr-only">
        Search films, people and studios
      </label>
      <input
        id="site-search"
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder="Search films, people, studios"
        className="w-full rounded-sm bg-row px-3 py-2 text-sm text-screen placeholder:text-haze/70 focus:bg-row-raised focus:outline-2 focus:outline-projector"
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

const navLinkClass =({ isActive }: { isActive: boolean }) =>
  `text-sm font-medium transition-colors hover:text-screen ${isActive ? "text-screen" : "text-haze"}`;

export function Layout() {
  const { user, logout } = useAuth();

  return (
    <div className="flex min-h-dvh flex-col">
      <ScrollRestoration />
      <header className="border-b border-white/5">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-8 gap-y-3 px-4 py-4 sm:px-6">
          <Link to="/" className="marquee text-3xl text-projector">
            Reelr
          </Link>

          <nav className="flex items-center gap-6" aria-label="Main">
            <NavLink to="/" end className={navLinkClass}>
              Films
            </NavLink>
          </nav>

          <div className="order-last w-full sm:order-none sm:ml-auto sm:w-auto">
            <SearchBox />
          </div>

          <div className="ml-auto flex items-center gap-4 sm:ml-0">
            {user ? (
              <>
                <NavLink
                  to="/profile"
                  aria-label={`Profile of ${user.username}`}
                  title="Your profile"
                  className={({ isActive }) =>
                    `flex items-center gap-2 rounded-full ${navLinkClass({ isActive })}`
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span
                        className={`flex size-8 items-center justify-center rounded-full transition-colors ${
                          isActive ? "bg-projector text-salon" : "bg-row text-screen hover:bg-row-raised"
                        }`}
                      >
                        <UserIcon />
                      </span>
                      <span className="hidden sm:inline">{user.username}</span>
                    </>
                  )}
                </NavLink>
                <button
                  type="button"
                  onClick={logout}
                  className="text-sm text-haze transition-colors hover:text-screen"
                >
                  Log out
                </button>
              </>
            ) : (
              <>
                <NavLink to="/login" className={navLinkClass}>
                  Log in
                </NavLink>
                <Link
                  to="/register"
                  className="rounded-sm bg-projector px-3 py-1.5 text-sm font-semibold text-salon transition hover:brightness-110"
                >
                  Create account
                </Link>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="mx-auto w-full max-w-6xl px-4 py-10 text-xs text-haze/70 sm:px-6">
        Film data and images from TMDB. Reelr is not endorsed or certified by TMDB.
      </footer>
    </div>
  );
}
