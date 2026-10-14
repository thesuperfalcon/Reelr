import { Link } from "react-router";
import { useAuth } from "../auth/auth-context";

// What Reelr is and how to join, for visitors who are not logged in. The start page otherwise opens on a film,
// which tells a newcomer nothing about the site.
export function Welcome() {
  const { user, restoring } = useAuth();

  if (user || restoring) {
    return null;
  }

  return (
    <section
      aria-label="About Reelr"
      className="mb-12 flex flex-col gap-5 rounded-sm border-l-2 border-projector bg-row px-5 py-6 sm:flex-row sm:items-center sm:justify-between sm:px-8"
    >
      <div className="max-w-prose">
        <p className="text-lg font-semibold text-screen">A film diary to share with your friends.</p>
        <p className="mt-1 text-sm text-haze">
          Log what you watch, rate and review films, make lists and see what your friends are watching. Reelr is
          invite only for now.
        </p>
      </div>
      <div className="flex shrink-0 flex-wrap gap-3">
        <Link to="/register" className="btn btn-primary">
          I have an invite
        </Link>
        <Link to="/login" className="btn btn-secondary">
          Log in
        </Link>
      </div>
    </section>
  );
}
