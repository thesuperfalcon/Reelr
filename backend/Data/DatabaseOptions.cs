namespace backend.Data;

// Bound from the "Database" configuration section.
public class DatabaseOptions
{
    // How long after the last use IdleConnectionCloser closes the pooled connections. 0 leaves them to the pool.
    public int CloseIdleConnectionsAfterSeconds { get; set; } = 60;
}
