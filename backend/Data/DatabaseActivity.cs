namespace backend.Data;

/// <summary>
/// Keeps track of when the app uses the database; DatabaseActivityInterceptor reports every connection opened or
/// closed. In Azure the serverless database pauses when idle, and every wake-up uses part of its free monthly amount.
/// So work that is not needed right away waits until the database is in use anyway (NextUseAsync), and cached
/// copies can tell whether anything may have changed since they were read (Version).
/// </summary>
public sealed class DatabaseActivity
{
    private long _version;
    private long _lastUsedAt = Environment.TickCount64;
    private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Goes up with every use, so a value saved earlier tells whether the database was used since.
    public long Version => Interlocked.Read(ref _version);

    public TimeSpan SinceLastUse => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastUsedAt));

    public void Used()
    {
        Interlocked.Exchange(ref _lastUsedAt, Environment.TickCount64);
        Interlocked.Increment(ref _version);
        Volatile.Read(ref _next).TrySetResult();
    }

    // Completes at the first use after the call, not for one that came earlier.
    public Task NextUseAsync(CancellationToken cancellationToken)
    {
        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _next, next);
        return next.Task.WaitAsync(cancellationToken);
    }
}
