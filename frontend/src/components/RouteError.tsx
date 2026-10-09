import { isRouteErrorResponse, Link, useRouteError } from "react-router";

// After a new deploy the old page's code files are gone, so opening a page that was not loaded yet fails.
function isStaleBuild(error: unknown) {
  return (
    error instanceof Error &&
    /dynamically imported module|Importing a module script failed|error loading dynamically imported module/i.test(
      error.message,
    )
  );
}

// Shown in place of a page that crashed while rendering or loading, instead of React Router's
// developer screen. The header and footer stay, so the rest of the site can still be reached.
export function RouteError() {
  const error = useRouteError();
  const staleBuild = isStaleBuild(error);

  if (import.meta.env.DEV) {
    console.error(error);
  }

  return (
    <div role="alert" className="mx-auto max-w-6xl px-4 py-20 sm:px-6">
      <h1 className="marquee text-6xl sm:text-8xl">{staleBuild ? "New version" : "Film snapped"}</h1>
      <p className="mt-6 max-w-prose text-haze">
        {staleBuild
          ? "Reelr was updated while this tab was open. Reload the page to get the new version."
          : isRouteErrorResponse(error)
            ? `This page could not be shown (${error.status}).`
            : "Something went wrong while showing this page. Reloading usually helps."}
      </p>
      <div className="mt-6 flex gap-6">
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="font-medium text-projector underline underline-offset-4"
        >
          Reload the page
        </button>
        <Link to="/" className="font-medium text-haze underline underline-offset-4 hover:text-screen">
          Go to films
        </Link>
      </div>
    </div>
  );
}
