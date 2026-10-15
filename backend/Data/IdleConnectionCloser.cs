using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace backend.Data;

/// <summary>
/// Closes the pooled database connections soon after the app stops using the database. An open connection is a
/// session, and a serverless Azure SQL database only starts counting down to its pause once no sessions are left.
/// The pool keeps idle connections open for 4 to 8 minutes, which made every wake-up that much longer (measured in
/// October 2026: about 7 of the 23 minutes a short visit kept the database awake).
/// Before closing, it runs the IBeforeDatabaseIdle work while the database is still awake.
/// </summary>
public class IdleConnectionCloser : BackgroundService
{
    private readonly DatabaseActivity _activity;
    private readonly IServiceScopeFactory _scopes;
    private readonly DatabaseOptions _options;
    private readonly ILogger<IdleConnectionCloser> _logger;

    public IdleConnectionCloser(
        DatabaseActivity activity,
        IServiceScopeFactory scopes,
        IOptions<DatabaseOptions> options,
        ILogger<IdleConnectionCloser> logger)
    {
        _activity = activity;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.CloseIdleConnectionsAfterSeconds <= 0)
        {
            return;
        }

        var idle = TimeSpan.FromSeconds(_options.CloseIdleConnectionsAfterSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Min(10, _options.CloseIdleConnectionsAfterSeconds)));

        // Start-up reads the data protection keys, so there is something to close from the start.
        long closedAt = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (_activity.Version == closedAt || _activity.SinceLastUse < idle)
            {
                continue;
            }

            await RunBeforeIdleAsync(stoppingToken);

            // Connections still in use are closed when they are returned, not taken from their request.
            SqlConnection.ClearAllPools();
            closedAt = _activity.Version;
        }
    }

    private async Task RunBeforeIdleAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopes.CreateScope();

        foreach (var work in scope.ServiceProvider.GetServices<IBeforeDatabaseIdle>())
        {
            try
            {
                await work.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The connections are closed anyway; the work runs again the next time the database goes idle.
                _logger.LogWarning(ex, "{Work} failed before the database went idle", work.GetType().Name);
            }
        }
    }
}
