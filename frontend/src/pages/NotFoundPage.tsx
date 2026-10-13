import { Link } from "react-router";
import { usePageTitle } from "../lib/title";

export function NotFoundPage() {
  usePageTitle("Page not found");
  return (
    <div className="page py-20">
      <p className="eyebrow">Error 404</p>
      <h1 className="marquee mt-3 text-7xl sm:text-9xl">Reel missing</h1>
      <p className="mt-6 max-w-prose text-haze">This page does not exist. The link may be old or mistyped.</p>
      <div className="mt-8 flex flex-wrap gap-3">
        <Link to="/" className="btn btn-primary">
          Go to films
        </Link>
        <Link to="/search" className="btn btn-secondary">
          Search
        </Link>
      </div>
    </div>
  );
}
