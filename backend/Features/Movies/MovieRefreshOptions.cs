namespace backend.Features.Movies
{
    // Bound from the "MovieRefresh" configuration section.
    public class MovieRefreshOptions
    {
        public bool Enabled { get; set; } = true;

        // How often the refresher wakes up.
        public double IntervalHours { get; set; } = 6;

        // A stored film older than this is copied from TMDB again.
        public int MaxAgeDays { get; set; } = 30;

        // At most this many films per run, so a large catalogue is spread over many runs and TMDB's quota.
        public int BatchSize { get; set; } = 100;
    }
}
