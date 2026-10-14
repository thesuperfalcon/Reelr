namespace backend.Features.Analytics;

/// <summary>
/// How often a kind of page was opened on one day. Only the route is stored, such as /movie/:tmdbId, never the
/// address with its ids, and nothing about the visitor: no account, IP address or cookie. See PageViewRoutes.
/// </summary>
public class PageView
{
    public const int RouteMaxLength = 64;

    // The UTC day.
    public DateOnly Day { get; set; }

    public string Route { get; set; } = null!;

    public int Views { get; set; }
}
