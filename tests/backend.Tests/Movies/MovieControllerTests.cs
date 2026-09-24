using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Movies;

public class MovieControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;
    private readonly HttpClient _client;

    public MovieControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static TmdbSearchResultDto SearchResult(params string[] titles) => new()
    {
        Page = 1,
        Results = titles.Select((title, i) => new TmdbSearchMovieDto { Id = i + 1, Title = title }).ToList()
    };

    // ---- Get movie ----

    [Fact]
    public async Task GetMovie_ReturnsTmdbMovie()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10001, "The Matrix");

        var movie = await _client.GetFromJsonAsync<TmdbMovieDto>($"/api/movie/{tmdbId}");

        Assert.Equal(tmdbId, movie!.Id);
        Assert.Equal("The Matrix", movie.Title);
        Assert.Equal("/10001.jpg", movie.PosterPath);
    }

    [Fact]
    public async Task GetMovie_SendsTokenToTmdb()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10002, "Authorized");

        await _client.GetAsync($"/api/movie/{tmdbId}");

        var request = _factory.Tmdb.Requests.Last(r => r.RequestUri!.AbsolutePath.EndsWith($"/movie/{tmdbId}"));
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetMovie_UnknownMovie_Returns404()
    {
        var response = await _client.GetAsync("/api/movie/10999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Search ----

    [Fact]
    public async Task SearchMovies_WithQuery_SearchesByTitle()
    {
        _factory.Tmdb.SetResponse("search/movie", SearchResult("Alien", "Aliens"));

        var result = await _client.GetFromJsonAsync<TmdbSearchResultDto>("/api/movie/search?query=alien%20isolation&page=2");

        Assert.Equal(["Alien", "Aliens"], result!.Results.Select(m => m.Title));
        var request = _factory.Tmdb.RequestsTo("search/movie").Last();
        Assert.Contains("query=alien%20isolation", request.Query);
        Assert.Contains("page=2", request.Query);
    }

    [Fact]
    public async Task SearchMovies_WithFilters_UsesDiscover()
    {
        _factory.Tmdb.SetResponse("discover/movie", SearchResult("Filtered"));

        var result = await _client.GetFromJsonAsync<TmdbSearchResultDto>("/api/movie/search?castId=11&crewId=22&studioId=33&genreId=44&page=3");

        Assert.Equal("Filtered", Assert.Single(result!.Results).Title);
        var query = _factory.Tmdb.RequestsTo("discover/movie").Last().Query;
        Assert.Contains("with_cast=11", query);
        Assert.Contains("with_crew=22", query);
        Assert.Contains("with_companies=33", query);
        Assert.Contains("with_genres=44", query);
        Assert.Contains("page=3", query);
    }

    [Fact]
    public async Task SearchMovies_WithSingleFilter_OnlySendsThatFilter()
    {
        _factory.Tmdb.SetResponse("discover/movie", SearchResult("Genre only"));

        await _client.GetAsync("/api/movie/search?genreId=878");

        var query = _factory.Tmdb.RequestsTo("discover/movie").Last().Query;
        Assert.Contains("with_genres=878", query);
        Assert.DoesNotContain("with_cast", query);
        Assert.DoesNotContain("with_crew", query);
        Assert.DoesNotContain("with_companies", query);
    }

    [Fact]
    public async Task SearchMovies_QueryAndFilters_QueryWins()
    {
        _factory.Tmdb.SetResponse("search/movie", SearchResult("By title"));
        var discoverRequestsBefore = _factory.Tmdb.RequestsTo("discover/movie").Count();

        var result = await _client.GetFromJsonAsync<TmdbSearchResultDto>("/api/movie/search?query=title&genreId=1");

        Assert.Equal("By title", Assert.Single(result!.Results).Title);
        Assert.Equal(discoverRequestsBefore, _factory.Tmdb.RequestsTo("discover/movie").Count());
    }

    [Fact]
    public async Task SearchMovies_WithoutQueryOrFilters_Returns400()
    {
        var response = await _client.GetAsync("/api/movie/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Search all ----

    [Fact]
    public async Task SearchAll_SplitsPeopleIntoCastAndCrewByPopularity()
    {
        _factory.Tmdb.SetResponse("search/movie", SearchResult());
        _factory.Tmdb.SetResponse("search/company", new TmdbCompanySearchResultDto { Results = [new() { Id = 1, Name = "Warner Bros." }] });
        _factory.Tmdb.SetResponse("search/person", new TmdbPersonSearchResultDto
        {
            Results =
            [
                new() { Id = 1, Name = "Keanu", KnownForDepartment = "Acting", Popularity = 5 },
                new() { Id = 2, Name = "Lana", KnownForDepartment = "Directing", Popularity = 9 },
                new() { Id = 3, Name = "Carrie-Anne", KnownForDepartment = "Acting", Popularity = 10 },
                new() { Id = 4, Name = "Bill", KnownForDepartment = "Camera", Popularity = 2 }
            ]
        });

        var result = await _client.GetFromJsonAsync<MovieSearchResultDto>("/api/movie/search/all?query=matrix");

        Assert.Equal(["Carrie-Anne", "Keanu"], result!.Cast.Select(p => p.Name));
        Assert.Equal(["Lana", "Bill"], result.Crew.Select(p => p.Name));
        Assert.Equal("Warner Bros.", Assert.Single(result.Studios).Name);
    }

    [Fact]
    public async Task SearchAll_ExactTitleMatchIsMovedToTop()
    {
        _factory.Tmdb.SetResponse("search/movie", SearchResult("The Matrix Reloaded", "The Matrix Resurrections", "The Matrix"));
        _factory.Tmdb.SetResponse("search/person", new TmdbPersonSearchResultDto());
        _factory.Tmdb.SetResponse("search/company", new TmdbCompanySearchResultDto());

        var result = await _client.GetFromJsonAsync<MovieSearchResultDto>("/api/movie/search/all?query=the%20matrix");

        Assert.Equal(["The Matrix", "The Matrix Reloaded", "The Matrix Resurrections"], result!.Movies.Select(m => m.Title));
    }

    [Theory]
    [InlineData("/api/movie/search/all?query=")]
    [InlineData("/api/movie/search/all?query=%20%20")]
    public async Task SearchAll_EmptyQuery_Returns400(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Lists ----

    [Theory]
    [InlineData("trending", "trending/movie/week")]
    [InlineData("popular", "movie/popular")]
    public async Task MovieLists_ReturnTmdbResults(string endpoint, string tmdbPath)
    {
        _factory.Tmdb.SetResponse(tmdbPath, SearchResult($"{endpoint} one", $"{endpoint} two"));

        var result = await _client.GetFromJsonAsync<TmdbSearchResultDto>($"/api/movie/{endpoint}");

        Assert.Equal([$"{endpoint} one", $"{endpoint} two"], result!.Results.Select(m => m.Title));
    }

    [Theory]
    [InlineData("recommended", "recommendations")]
    [InlineData("similar", "similar")]
    public async Task RelatedMovies_ReturnTmdbResults(string endpoint, string tmdbSuffix)
    {
        _factory.Tmdb.SetResponse($"movie/10003/{tmdbSuffix}", SearchResult("Related"));

        var result = await _client.GetFromJsonAsync<TmdbSearchResultDto>($"/api/movie/10003/{endpoint}");

        Assert.Equal("Related", Assert.Single(result!.Results).Title);
    }

    [Theory]
    [InlineData("recommended")]
    [InlineData("similar")]
    [InlineData("credits")]
    [InlineData("details")]
    public async Task MovieSubresources_UnknownMovie_Returns404(string endpoint)
    {
        var response = await _client.GetAsync($"/api/movie/10998/{endpoint}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("recommended")]
    [InlineData("similar")]
    [InlineData("details")]
    public async Task MovieSubresources_NonPositiveId_Returns400(string endpoint)
    {
        var response = await _client.GetAsync($"/api/movie/0/{endpoint}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Credits ----

    [Fact]
    public async Task GetCredits_ReturnsCastAndCrew()
    {
        _factory.Tmdb.SetResponse("movie/10004/credits", new TmdbCreditsDto
        {
            Cast = [new() { Id = 1, Name = "Keanu Reeves", Character = "Neo" }],
            Crew = [new() { Id = 2, Name = "Lana Wachowski", Job = "Director" }]
        });

        var credits = await _client.GetFromJsonAsync<TmdbCreditsDto>("/api/movie/10004/credits");

        Assert.Equal("Neo", Assert.Single(credits!.Cast).Character);
        Assert.Equal("Director", Assert.Single(credits.Crew).Job);
    }

    // ---- Details ----

    [Fact]
    public async Task GetMovieDetails_CombinesMovieCreditsVideosAndImages()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10005, "Detailed");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/credits", new TmdbCreditsDto
        {
            Cast = [new() { Id = 1, Name = "Lead", Order = 0 }],
            Crew = [new() { Id = 2, Name = "Boss", Job = "Director" }]
        });
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/videos", new TmdbVideosDto
        {
            Results = [new() { Key = "abc", Site = "YouTube", Type = "Trailer", Official = true }]
        });
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/images", new TmdbImagesDto
        {
            Backdrops = [new() { FilePath = "/backdrop.jpg", Width = 1920, Height = 1080, AspectRatio = 1.778 }]
        });

        var details = await _client.GetFromJsonAsync<MovieDetailsDto>($"/api/movie/{tmdbId}/details");

        Assert.NotNull(details);
        Assert.Equal(tmdbId, details.Id);
        Assert.Equal("Detailed", details.Title);
        Assert.Equal(136, details.Runtime);
        Assert.Equal("Lead", Assert.Single(details.Cast).Name);
        Assert.Equal("Boss", Assert.Single(details.Crew).Name);
        Assert.Equal("abc", Assert.Single(details.Videos).Key);
        var image = Assert.Single(details.Images);
        Assert.Equal("/backdrop.jpg", image.FilePath);
        Assert.Equal(1920, image.Width);
    }

    [Fact]
    public async Task GetMovieDetails_CastIsOrderedAndLimitedToTen()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10006, "Big cast");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/credits", new TmdbCreditsDto
        {
            Cast = Enumerable.Range(0, 12).Reverse()
                .Select(order => new TmdbCastDto { Id = order, Name = $"Actor {order}", Order = order })
                .ToList()
        });

        var details = await _client.GetFromJsonAsync<MovieDetailsDto>($"/api/movie/{tmdbId}/details");

        Assert.Equal(Enumerable.Range(0, 10), details!.Cast.Select(c => c.Order));
    }

    [Fact]
    public async Task GetMovieDetails_CrewOnlyContainsRelevantJobs()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10007, "Crewed");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/credits", new TmdbCreditsDto
        {
            Crew =
            [
                new() { Id = 1, Name = "Director", Job = "Director" },
                new() { Id = 2, Name = "Grip", Job = "Key Grip" },
                new() { Id = 3, Name = "Composer", Job = "original music composer" },
                new() { Id = 4, Name = "No job", Job = null },
                new() { Id = 5, Name = "Writer", Job = "Screenplay" }
            ]
        });

        var details = await _client.GetFromJsonAsync<MovieDetailsDto>($"/api/movie/{tmdbId}/details");

        Assert.Equal(["Director", "Composer", "Writer"], details!.Crew.Select(c => c.Name));
    }

    [Fact]
    public async Task GetMovieDetails_VideosAreYouTubeTrailersAndTeasersOfficialFirst()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10008, "Trailers");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/videos", new TmdbVideosDto
        {
            Results =
            [
                new() { Key = "teaser-unofficial", Site = "YouTube", Type = "Teaser", Official = false },
                new() { Key = "vimeo-trailer", Site = "Vimeo", Type = "Trailer", Official = true },
                new() { Key = "clip", Site = "YouTube", Type = "Clip", Official = true },
                new() { Key = "trailer-unofficial", Site = "YouTube", Type = "Trailer", Official = false },
                new() { Key = "teaser-official", Site = "YouTube", Type = "Teaser", Official = true },
                new() { Key = "trailer-official", Site = "YouTube", Type = "Trailer", Official = true }
            ]
        });

        var details = await _client.GetFromJsonAsync<MovieDetailsDto>($"/api/movie/{tmdbId}/details");

        Assert.Equal(
            ["trailer-official", "teaser-official", "trailer-unofficial", "teaser-unofficial"],
            details!.Videos.Select(v => v.Key));
    }

    [Fact]
    public async Task GetMovieDetails_ImagesAreBackdropsThenPostersLimitedToTwenty()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10009, "Pictures");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/images", new TmdbImagesDto
        {
            Backdrops = Enumerable.Range(1, 15).Select(i => new TmdbImageDto { FilePath = $"/backdrop{i}.jpg" }).ToList(),
            Posters = Enumerable.Range(1, 10).Select(i => new TmdbImageDto { FilePath = $"/poster{i}.jpg" }).ToList()
        });

        var details = await _client.GetFromJsonAsync<MovieDetailsDto>($"/api/movie/{tmdbId}/details");

        Assert.Equal(20, details!.Images.Count);
        Assert.Equal("/backdrop1.jpg", details.Images[0].FilePath);
        Assert.Equal("/poster5.jpg", details.Images[^1].FilePath);
    }

    [Fact]
    public async Task GetMovieDetails_ExtrasFailing_StillReturnsMovie()
    {
        var tmdbId = _factory.Tmdb.AddMovie(10010, "Partial");
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/credits", null, HttpStatusCode.InternalServerError);
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/videos", null, HttpStatusCode.InternalServerError);
        _factory.Tmdb.SetResponse($"movie/{tmdbId}/images", null, HttpStatusCode.InternalServerError);

        var response = await _client.GetAsync($"/api/movie/{tmdbId}/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<MovieDetailsDto>();
        Assert.Equal("Partial", details!.Title);
        Assert.Empty(details.Cast);
        Assert.Empty(details.Crew);
        Assert.Empty(details.Videos);
        Assert.Empty(details.Images);
    }

    [Fact]
    public async Task GetMovieDetails_TmdbError_Returns502()
    {
        _factory.Tmdb.SetResponse("movie/10011", null, HttpStatusCode.ServiceUnavailable);

        var response = await _client.GetAsync("/api/movie/10011/details");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
