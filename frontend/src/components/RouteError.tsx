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
    <div role="alert" className="page py-20">
      <p className="eyebrow">Something went wrong</p>
      <h1 className="heading-display mt-3">{staleBuild ? "New version" : "Film snapped"}</h1>
      <p className="mt-6 max-w-prose text-haze">
        {staleBuild
          ? "Reelr was updated while this tab was open. Reload the page to get the new version."
          : isRouteErrorResponse(error)
            ? `This page could not be shown (${error.status}).`
            : "Something went wrong while showing this page. Reloading usually helps."}
      </p>
      <div className="mt-8 flex flex-wrap gap-3">
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="btn btn-primary"
        >
          Reload the page
        </button>
        <Link to="/" className="btn btn-secondary">
          Go to films
        </Link>
      </div>
    </div>
  );
}
