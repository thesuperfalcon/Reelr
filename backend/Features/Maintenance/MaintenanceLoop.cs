using backend.Data;
using backend.Features.Auth;
using backend.Features.Movies;
using Microsoft.Extensions.Options;

namespace backend.Features.Maintenance
{
    // Housekeeping every few hours: copies a batch of the oldest films from TMDB again (MovieCatalog.RefreshStaleAsync)
    // and deletes every expired refresh token (RefreshTokens.DeleteExpiredAsync), also for members who no longer sign
    // in. A run waits until the app uses the database anyway (DatabaseActivity), so the app can stay up all the time
    // without waking a paused database. It does not wait for any request: scanners asking for /api/.env would count,
    // and the run would wake the database for them. With several app instances each would do this; that is harmless,
    // only wasteful, and can move to a single scheduled job when there is more than one instance.
    public class MaintenanceLoop : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly DatabaseActivity _database;
        private readonly MovieRefreshOptions _options;
        private readonly ILogger<MaintenanceLoop> _logger;

        public MaintenanceLoop(
            IServiceScopeFactory scopes,
            DatabaseActivity database,
            IOptions<MovieRefreshOptions> options,
            ILogger<MaintenanceLoop> logger)
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
                await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
            }
        }

        /// <summary>One run of every task. A task that fails is logged and does not stop the others.</summary>
        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            await TryAsync("Refreshing films from TMDB failed", async services =>
            {
                var refreshed = await services.GetRequiredService<MovieCatalog>().RefreshStaleAsync(
                    TimeSpan.FromDays(_options.MaxAgeDays), _options.BatchSize, cancellationToken);

                if (refreshed > 0)
                {
                    _logger.LogInformation("Refreshed {Count} films from TMDB", refreshed);
                }
            });

            await TryAsync("Deleting expired refresh tokens failed", async services =>
            {
                var deleted = await services.GetRequiredService<RefreshTokens>().DeleteExpiredAsync(cancellationToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("Deleted {Count} expired refresh tokens", deleted);
                }
            });
        }

        private async Task TryAsync(string failure, Func<IServiceProvider, Task> task)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await task(scope.ServiceProvider);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The next run tries again; the app keeps working with the data it has.
                _logger.LogWarning(ex, failure);
            }
        }
    }
}
