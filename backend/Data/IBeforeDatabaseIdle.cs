namespace backend.Data;

/// <summary>
/// Work that IdleConnectionCloser runs while the database is still awake, just before it lets the database pause,
/// such as bringing a cached copy up to date so later visitors do not wake the database for it.
/// Registered as scoped, so it can use the DbContext.
/// </summary>
public interface IBeforeDatabaseIdle
{
    Task RunAsync(CancellationToken cancellationToken);
}
