using System.Net;
using System.Net.Http.Json;

namespace backend.Tests.Infrastructure;

public class HealthCheckTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public HealthCheckTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Live_AnswersWithoutCheckingAnything()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<HealthResponse>())!;
        Assert.Equal("Healthy", health.Status);
        Assert.Empty(health.Checks);
    }

    [Fact]
    public async Task Ready_ChecksTheDatabaseAndTmdb()
    {
        _factory.Tmdb.SetResponse("configuration", new { });

        var response = await _factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<HealthResponse>())!;
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("Healthy", health.Checks["database"]);
        Assert.Equal("Healthy", health.Checks["tmdb"]);
    }
}

// A separate host, since the TMDB result is kept for a minute.
public class HealthCheckTmdbDownTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public HealthCheckTmdbDownTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ready_TmdbDown_IsDegradedButStillReady()
    {
        _factory.Tmdb.SetResponse("configuration", null, HttpStatusCode.InternalServerError);

        var response = await _factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<HealthResponse>())!;
        Assert.Equal("Degraded", health.Status);
        Assert.Equal("Healthy", health.Checks["database"]);
        Assert.Equal("Degraded", health.Checks["tmdb"]);
    }
}

public record HealthResponse(string Status, Dictionary<string, string> Checks);
