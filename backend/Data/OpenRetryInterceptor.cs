using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace backend.Data;

/// <summary>
/// Retries opening a connection when SQL Server reports a transient error. A paused Azure SQL serverless database
/// refuses the first connection (error 40613) while it wakes up, which takes up to a minute. Only opening the
/// connection is retried, so queries and transactions behave as before.
/// </summary>
public class OpenRetryInterceptor : DbConnectionInterceptor
{
    private static readonly SqlRetryLogicBaseProvider Retry = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(
        new SqlRetryLogicOption
        {
            NumberOfTries = 6,
            DeltaTime = TimeSpan.FromSeconds(2),
            MaxTimeInterval = TimeSpan.FromSeconds(20),
            // Not the factory's default list: that includes 4060 ("cannot open database"), which is how EF finds
            // out that a database does not exist yet, so the migrations could no longer create one.
            TransientErrors =
            [
                40613, // database not currently available (paused serverless database waking up)
                40197, // service error, e.g. during an upgrade or failover
                40501, // service busy
                49918, 49919, 49920, // not enough resources
                10928, 10929 // resource limits reached
            ]
        });

    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        UseRetry(connection);
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        UseRetry(connection);
        return ValueTask.FromResult(result);
    }

    private static void UseRetry(DbConnection connection)
    {
        if (connection is SqlConnection sql)
        {
            sql.RetryLogicProvider = Retry;
        }
    }
}
