import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/auth-context";

// The pages where a visitor is already logging in or joining.
const accountPages = ["/login", "/register", "/forgot-password", "/reset-password", "/confirm-email"];

// On phones the header's sign-up link scrolls away, so visitors who are not logged in keep a way to join at the
// bottom of the screen. Hidden from small-screen breakpoint up, where the header link is enough. index.css keeps
// content and focused controls clear of it.
export function JoinBar() {
  const { user, restoring } = useAuth();
  const { pathname } = useLocation();

  if (user || restoring || accountPages.includes(pathname)) {
    return null;
  }

  return (
    <aside
      aria-label="Join Reelr"
      className="mobile-join-bar fixed inset-x-0 bottom-0 z-40 flex items-center justify-between gap-4 border-t border-rule/10 bg-salon/95 px-4 py-3 backdrop-blur sm:hidden"
    >
      <p className="text-sm text-screen">Have an invite?</p>
      <Link to="/register" className="btn btn-sm btn-primary">
        Create account
      </Link>
    </aside>
  );
}
