namespace backend.Features.Movies
{
    // Bound from the "MovieRefresh" configuration section. Enabled and IntervalHours apply to the whole MaintenanceLoop.
    public class MovieRefreshOptions
    {
        public bool Enabled { get; set; } = true;

        // The shortest time between two runs. A run waits for the database to be in use after that, so it never wakes it.
        public double IntervalHours { get; set; } = 6;

        // How long a run waits after that use, so it does not slow down the first requests of a visit.
        public double StartDelayMinutes { get; set; } = 2;

        // A stored film older than this is copied from TMDB again.
        public int MaxAgeDays { get; set; } = 30;

        // At most this many films per run, so a large catalogue is spread over many runs and TMDB's quota.
        public int BatchSize { get; set; } = 100;
    }
}
