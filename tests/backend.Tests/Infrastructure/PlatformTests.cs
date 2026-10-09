using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace backend.Tests.Infrastructure;

/// <summary>
/// App-wide behavior that is not tied to one feature: health check and error response format.
/// </summary>
public class PlatformTests : IClassFixture<ReelrApiFactory>
{
    private readonly HttpClient _client;

    public PlatformTests(ReelrApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_DatabaseUp_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Startup_WithoutOptionalSettings_Works()
    {
        // Production sets only the required secrets; optional sections such as Tmdb:Resilience are absent.
        await using var factory = new ProductionDefaultsApiFactory();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Jwt:Key", "too-short")]
    [InlineData("Jwt:Key", "")]
    [InlineData("TMDB_READ_ACCESS_TOKEN", "")]
    public void Startup_MissingOrWeakSecret_Fails(string key, string value)
    {
        using var factory = new ReelrApiFactory().WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public async Task ProtectedEndpointWithoutToken_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public class ProductionDefaultsApiFactory : ReelrApiFactory
{
    protected override bool OverrideTmdbResilience => false;
}
