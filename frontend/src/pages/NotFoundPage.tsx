import { Link } from "react-router";

export function NotFoundPage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="marquee text-7xl sm:text-9xl">Reel missing</h1>
      <p className="mt-6 max-w-prose text-haze">This page does not exist. The link may be old or mistyped.</p>
      <Link to="/" className="mt-6 inline-block font-medium text-projector underline underline-offset-4">
        Go to films
      </Link>
    </div>
  );
}
