using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;
using backend.Features.Ratings.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Movies;

/// <summary>
/// How the API behaves when TMDB fails, is slow, or gets input it should never be sent.
/// </summary>
public class TmdbResilienceTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;
    private readonly HttpClient _client;

    public TmdbResilienceTests(ReelrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        // TMDB's own error text stays in the log.
        Assert.DoesNotContain("TMDB returned", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_TmdbError_LogsThePathWithoutTheSearchText()
    {
        _factory.Tmdb.SetResponse("search/person", null, HttpStatusCode.InternalServerError);

        await _client.GetAsync("/api/people/search?query=secret-search-text");

        Assert.Contains(_factory.Logs.Entries, e => Equals(e.State.GetValueOrDefault("Path"), "search/person"));
        Assert.DoesNotContain(_factory.Logs.Entries, e => e.Message.Contains("secret-search-text"));
    }

    [Fact]
    public async Task Popular_TmdbError_Returns502()
    {
        _factory.Tmdb.SetResponse("movie/popular", null, HttpStatusCode.InternalServerError);

        await AssertProblemAsync(await _client.GetAsync("/api/movies/popular"), HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Search_TmdbTooSlow_Returns503()
    {
        _factory.Tmdb.SetDelay("search/company", TimeSpan.FromSeconds(10));

        await AssertProblemAsync(await _client.GetAsync("/api/companies/search?query=slow"), HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Search_TmdbUnreadableJson_Returns502()
    {
        _factory.Tmdb.SetResponse("search/person", "not an object");

        await AssertProblemAsync(await _client.GetAsync("/api/people/search?query=broken"), HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Trending_TmdbFailsOnce_IsRetried()
    {
        _factory.Tmdb.SetResponse("trending/movie/week", new TmdbSearchResultDto());
        _factory.Tmdb.FailNext("trending/movie/week", 1);

        var response = await _client.GetAsync("/api/movies/trending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _factory.Tmdb.RequestsTo("trending/movie/week").Count());
    }

    [Fact]
    public async Task Rating_TmdbDownForUnstoredFilm_Returns502()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        _factory.Tmdb.SetResponse("movie/7101", null, HttpStatusCode.InternalServerError);

        var response = await user.Client.PostAsJsonAsync("/api/movies/7101/rating", new CreateRatingDto { Score = 4 });

        await AssertProblemAsync(response, HttpStatusCode.BadGateway);
    }

    [Theory]
    [InlineData("/api/movies/search?query={long}")]
    [InlineData("/api/movies/search/all?query={long}")]
    [InlineData("/api/people/search?query={long}")]
    [InlineData("/api/companies/search?query={long}")]
    [InlineData("/api/movies/search?query=matrix&page=0")]
    [InlineData("/api/movies/search?query=matrix&page=501")]
    public async Task Search_InputOutsideLimits_Returns400WithoutCallingTmdb(string url)
    {
        var before = _factory.Tmdb.Requests.Count;

        var response = await _client.GetAsync(url.Replace("{long}", new string('a', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, _factory.Tmdb.Requests.Count);
    }
}

public class TmdbCacheApiFactory : ReelrApiFactory
{
    protected override int TmdbCacheMegabytes => 50;
}

public class TmdbCacheTests : IClassFixture<TmdbCacheApiFactory>
{
    private readonly TmdbCacheApiFactory _factory;
    private readonly HttpClient _client;

    public TmdbCacheTests(TmdbCacheApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Popular_AskedTwice_CallsTmdbOnce()
    {
        _factory.Tmdb.SetResponse("movie/popular", new TmdbSearchResultDto());

        (await _client.GetAsync("/api/movies/popular")).EnsureSuccessStatusCode();
        (await _client.GetAsync("/api/movies/popular")).EnsureSuccessStatusCode();

        Assert.Single(_factory.Tmdb.RequestsTo("movie/popular"));
    }

    [Fact]
    public async Task UnknownMovie_IsNotCached()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/movies/7201")).StatusCode);
        _factory.Tmdb.AddMovie(7201, "Added later");

        var response = await _client.GetAsync("/api/movies/7201");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Failure_IsNotCached()
    {
        _factory.Tmdb.SetResponse("search/movie", new TmdbSearchResultDto());
        _factory.Tmdb.FailNext("search/movie", 3);

        var failed = await _client.GetAsync("/api/movies/search?query=retry");
        var later = await _client.GetAsync("/api/movies/search?query=retry");

        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
    }
}
