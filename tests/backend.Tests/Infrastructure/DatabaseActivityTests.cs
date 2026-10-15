using backend.Data;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Infrastructure;

public class DatabaseActivityTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public DatabaseActivityTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Waits_for_the_next_use()
    {
        var activity = new DatabaseActivity();

        var waiting = activity.NextUseAsync(CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        activity.Used();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_use_before_the_wait_does_not_count()
    {
        var activity = new DatabaseActivity();
        activity.Used();

        var waiting = activity.NextUseAsync(CancellationToken.None);

        Assert.False(waiting.IsCompleted);
    }

    [Fact]
    public async Task A_request_that_reads_the_database_counts_as_a_use()
    {
        var activity = _factory.Services.GetRequiredService<DatabaseActivity>();
        var before = activity.Version;

        await _factory.CreateClient().GetAsync("/api/feed/community");

        Assert.True(activity.Version > before);
        Assert.True(activity.SinceLastUse < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_request_that_does_not_read_the_database_does_not_count()
    {
        var activity = _factory.Services.GetRequiredService<DatabaseActivity>();
        await _factory.CreateClient().GetAsync("/health/live");
        var before = activity.Version;

        await _factory.CreateClient().GetAsync("/health/live");
        await _factory.CreateClient().GetAsync("/api/no-such-endpoint");

        Assert.Equal(before, activity.Version);
    }
}
