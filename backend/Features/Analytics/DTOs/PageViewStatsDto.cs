namespace backend.Features.Analytics.DTOs
{
    public class PageViewStatsDto
    {
        public int Days { get; set; }

        public int TotalViews { get; set; }

        // Views per UTC day, oldest first. Days without views are left out.
        public List<DayViewsDto> PerDay { get; set; } = [];

        // Views per route, the most viewed first.
        public List<RouteViewsDto> PerRoute { get; set; } = [];
    }

    public class DayViewsDto
    {
        public DateOnly Day { get; set; }

        public int Views { get; set; }
    }

    public class RouteViewsDto
    {
        public string Route { get; set; } = null!;

        public int Views { get; set; }
    }
}
