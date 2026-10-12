using System.Net;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace backend.Tests.Movies;

// Tmdb:BaseUrl written without the trailing slash, as someone could easily set it.
public class TmdbBaseUrlWithoutSlashApiFactory : ReelrApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Tmdb:BaseUrl", "http://tmdb.test/3");
    }
}

public class TmdbBaseUrlTests : IClassFixture<TmdbBaseUrlWithoutSlashApiFactory>
{
    private readonly TmdbBaseUrlWithoutSlashApiFactory _factory;

    public TmdbBaseUrlTests(TmdbBaseUrlWithoutSlashApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BaseUrlWithoutTrailingSlash_KeepsItsLastSegment()
    {
        var tmdbId = _factory.Tmdb.AddMovie(4242, "Slashless");

        var response = await _factory.CreateClient().GetAsync($"/api/movies/{tmdbId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = Assert.Single(_factory.Tmdb.Requests);
        Assert.Equal($"http://tmdb.test/3/movie/{tmdbId}", request.RequestUri!.GetLeftPart(UriPartial.Path));
    }
}
