namespace backend.Features.Analytics;

/// <summary>
/// The SPA's routes (frontend/src/main.tsx) that page views are counted for, written the way the SPA sends them.
/// Anything else is ignored, so the table cannot fill with made-up paths. "/*" is the "not found" page.
/// </summary>
public static class PageViewRoutes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "/",
        "/activity",
        "/movie/:tmdbId",
        "/person/:personId",
        "/list/:listId",
        "/review/:reviewId",
        "/search",
        "/user/:userId",
        "/login",
        "/register",
        "/confirm-email",
        "/forgot-password",
        "/reset-password",
        "/privacy",
        "/terms",
        "/accessibility",
        "/contact",
        "/profile",
        "/settings",
        "/admin",
        "/*"
    };
}
