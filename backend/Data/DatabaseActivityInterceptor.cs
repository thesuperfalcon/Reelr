using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace backend.Data;

/// <summary>
/// Reports every connection the app opens or closes to DatabaseActivity. Opened, not opening: only a connection
/// that succeeded means the database is awake.
/// </summary>
public class DatabaseActivityInterceptor : DbConnectionInterceptor
{
    private readonly DatabaseActivity _activity;

    public DatabaseActivityInterceptor(DatabaseActivity activity)
    {
        _activity = activity;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        _activity.Used();

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _activity.Used();
        return Task.CompletedTask;
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) =>
        _activity.Used();

    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
    {
        _activity.Used();
        return Task.CompletedTask;
    }
}
