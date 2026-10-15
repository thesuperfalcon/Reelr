using System.Net;
using backend.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Infrastructure;

public class IdleCloserApiFactory : ReelrApiFactory
{
    public BeforeIdleRuns Runs { get; } = new();

    protected override int CloseIdleConnectionsAfterSeconds => 1;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Runs);
            services.AddScoped<IBeforeDatabaseIdle, CountingBeforeIdle>();
        });
    }

    public class BeforeIdleRuns
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Add() => Interlocked.Increment(ref _count);
    }

    private class CountingBeforeIdle : IBeforeDatabaseIdle
    {
        private readonly BeforeIdleRuns _runs;

        public CountingBeforeIdle(BeforeIdleRuns runs)
        {
            _runs = runs;
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            _runs.Add();
            return Task.CompletedTask;
        }
    }
}

public class IdleConnectionCloserTests : IClassFixture<IdleCloserApiFactory>
{
    private readonly IdleCloserApiFactory _factory;

    public IdleConnectionCloserTests(IdleCloserApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Closes_once_after_each_use_and_the_database_still_works_afterwards()
    {
        var client = _factory.CreateClient();
        await client.GetAsync("/api/feed/community");
        var settled = await SettledCountAsync();

        var response = await client.GetAsync("/api/feed/community");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitUntilAsync(() => _factory.Runs.Count > settled);

        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(settled + 1, _factory.Runs.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/feed/community")).StatusCode);
    }

    // Waits until the closer has caught up with start-up and earlier requests: no new run for a few seconds.
    private async Task<int> SettledCountAsync()
    {
        await WaitUntilAsync(() => _factory.Runs.Count > 0);

        while (true)
        {
            var count = _factory.Runs.Count;
            await Task.Delay(TimeSpan.FromSeconds(2.5));
            if (_factory.Runs.Count == count)
            {
                return count;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the idle connection closer.");
            await Task.Delay(100);
        }
    }
}
