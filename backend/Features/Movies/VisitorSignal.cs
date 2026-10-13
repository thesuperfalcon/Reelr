namespace backend.Features.Movies
{
    // Tells background work that a visitor is using the site, so the database is awake anyway. In Azure the
    // database pauses when nobody uses it, and waking it only for background work would use up the free amount.
    // Health checks are not visitors: Program.cs only signals for /api and /auth requests.
    public sealed class VisitorSignal
    {
        private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Seen() => Volatile.Read(ref _next).TrySetResult();

        // Completes at the first visitor after the call, not for one who came earlier.
        public Task NextVisitorAsync(CancellationToken cancellationToken)
        {
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _next, next);
            return next.Task.WaitAsync(cancellationToken);
        }
    }
}
