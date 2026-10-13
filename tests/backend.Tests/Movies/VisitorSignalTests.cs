using backend.Features.Movies;

namespace backend.Tests.Movies;

public class VisitorSignalTests
{
    [Fact]
    public async Task Waits_for_the_next_visitor()
    {
        var signal = new VisitorSignal();

        var waiting = signal.NextVisitorAsync(CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        signal.Seen();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_visitor_before_the_wait_does_not_count()
    {
        var signal = new VisitorSignal();
        signal.Seen();

        var waiting = signal.NextVisitorAsync(CancellationToken.None);

        Assert.False(waiting.IsCompleted);
    }
}
