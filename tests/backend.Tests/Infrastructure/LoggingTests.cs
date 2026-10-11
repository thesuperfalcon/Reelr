using System.Net;
using System.Text.Json;
using backend.Features.Observability;

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

        var isHealthCheck = RequestTo("/health");
        Assert.DoesNotContain(_factory.Logs.Entries, e => isHealthCheck(e));
    }

    [Fact]
    public async Task CorrelationId_IsMadeWhenMissingAndSentBack()
    {
        var response = await _factory.CreateClient().GetAsync("/api/movie/trending");

        var id = Assert.Single(response.Headers.GetValues(CorrelationId.Header));
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task CorrelationId_FromTheProxyIsKeptAndOnTheRequestLog()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationId.Header, "nginx-request-42");

        var response = await client.GetAsync("/api/movie/popular");

        Assert.Equal("nginx-request-42", Assert.Single(response.Headers.GetValues(CorrelationId.Header)));
        var entry = Assert.Single(await _factory.Logs.WaitForAsync(RequestTo("/api/movie/popular")));
        Assert.Equal("nginx-request-42", entry.State[CorrelationId.LogProperty]);
    }

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("semi;colon")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task CorrelationId_ThatIsNotPlainIsReplaced(string sent)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationId.Header, sent);

        var response = await client.GetAsync("/api/movie/trending");

        Assert.NotEqual(sent, Assert.Single(response.Headers.GetValues(CorrelationId.Header)));
    }

    [Fact]
    public async Task CorrelationId_IsInErrorResponses()
    {
        var response = await _factory.CreateClient().GetAsync("/api/movies/0/reviews?cursor=broken");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            Assert.Single(response.Headers.GetValues(CorrelationId.Header)),
            problem.RootElement.GetProperty("correlationId").GetString());
    }
}
