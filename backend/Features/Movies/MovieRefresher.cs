using backend.Data;
using Microsoft.Extensions.Options;

namespace backend.Features.Movies
{
    // Keeps the stored film data from going stale: every few hours it copies a batch of the oldest films from
    // TMDB again (MovieCatalog.RefreshStaleAsync). A run waits until the app uses the database anyway
    // (DatabaseActivity), so the app can stay up all the time without waking a paused database. It does not wait
    // for any request: scanners asking for /api/.env would count, and the run would wake the database for them.
    // With several app instances each would do this; that is harmless, only wasteful, and can move to a single
    // scheduled job when there is more than one instance.
    public class MovieRefresher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly DatabaseActivity _database;
        private readonly MovieRefreshOptions _options;
        private readonly ILogger<MovieRefresher> _logger;

        public MovieRefresher(
            IServiceScopeFactory scopes,
            DatabaseActivity database,
            IOptions<MovieRefreshOptions> options,
            ILogger<MovieRefresher> logger)
        {
            _scopes = scopes;
            _database = database;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                return;
            }

            while (true)
            {
                await _database.NextUseAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(_options.StartDelayMinutes), stoppingToken);

                try
                {
                    using var scope = _scopes.CreateScope();
                    var catalog = scope.ServiceProvider.GetRequiredService<MovieCatalog>();
                    var refreshed = await catalog.RefreshStaleAsync(
                        TimeSpan.FromDays(_options.MaxAgeDays), _options.BatchSize, stoppingToken);

                    if (refreshed > 0)
                    {
                        _logger.LogInformation("Refreshed {Count} films from TMDB", refreshed);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The next run tries again; the app keeps working with the data it has.
                    _logger.LogWarning(ex, "Refreshing films from TMDB failed");
                }

                await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
            }
        }
    }
}
