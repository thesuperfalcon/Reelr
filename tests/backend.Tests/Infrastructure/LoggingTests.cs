namespace backend.Tests.Infrastructure;

public class LoggingTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public LoggingTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static Func<TestLogs.LogEntry, bool> RequestTo(string path) =>
        e => e.Category == "Serilog.AspNetCore.RequestLoggingMiddleware" && Equals(e.State.GetValueOrDefault("RequestPath"), path);

    [Fact]
    public async Task Request_IsLoggedWithStatusAndUserButWithoutQueryString()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        await user.Client.GetAsync("/api/users/search?query=private-words");

        var entry = Assert.Single(await _factory.Logs.WaitForAsync(RequestTo("/api/users/search")));
        Assert.Equal(200, entry.State["StatusCode"]);
        Assert.Equal(user.Id, entry.State["UserId"]);
        Assert.DoesNotContain(_factory.Logs.Entries, e => e.Message.Contains("private-words"));
    }

    [Fact]
    public async Task HealthCheck_IsNotLoggedWhenItPasses()
    {
        await _factory.CreateClient().GetAsync("/health");
        // A later request is logged, so the health check's line would have been written by then.
        await _factory.CreateClient().GetAsync("/api/movie/trending");
        await _factory.Logs.WaitForAsync(RequestTo("/api/movie/trending"));

        Assert.Empty(_factory.Logs.Entries.Where(RequestTo("/health")));
    }
}
