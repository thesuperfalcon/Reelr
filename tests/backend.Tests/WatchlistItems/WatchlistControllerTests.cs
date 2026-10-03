using System.Net;
using System.Net.Http.Json;
using backend.Features.Ratings.DTOs;
using backend.Features.WatchedMovies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.WatchlistItems;

public class WatchlistControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public WatchlistControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task AddAsync(HttpClient client, int tmdbId)
    {
        var response = await client.PostAsync($"/api/watchlist/{tmdbId}", null);
        response.EnsureSuccessStatusCode();
    }

    private static Task<List<WatchlistResponse>?> GetWatchlistAsync(HttpClient client) =>
        client.GetFromJsonAsync<List<WatchlistResponse>>("/api/watchlist");

    private record WatchlistResponse(int TmdbId, string Title, string? PosterUrl, DateTime AddedAt);

    [Fact]
    public async Task Watchlist_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var getResponse = await client.GetAsync("/api/watchlist");
        var postResponse = await client.PostAsync("/api/watchlist/1", null);
        var deleteResponse = await client.DeleteAsync("/api/watchlist/1");

        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deleteResponse.StatusCode);
    }

    // ---- Add ----

    [Fact]
    public async Task AddToWatchlist_NewMovie_AppearsInWatchlist()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9001, "To watch");

        var response = await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await GetWatchlistAsync(user.Client))!);
        Assert.Equal(tmdbId, item.TmdbId);
        Assert.Equal("To watch", item.Title);
        Assert.Equal("/9001.jpg", item.PosterUrl);
    }

    [Fact]
    public async Task AddToWatchlist_AlreadyAdded_Returns409()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9002, "Twice");
        await AddAsync(user.Client, tmdbId);

        var response = await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddToWatchlist_UnknownTmdbMovie_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsync("/api/watchlist/9999", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Get ----

    [Fact]
    public async Task GetWatchlist_ReturnsOwnMoviesNewestFirst()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var firstId = _factory.Tmdb.AddMovie(9003, "Older");
        var secondId = _factory.Tmdb.AddMovie(9004, "Newer");
        var bobsId = _factory.Tmdb.AddMovie(9005, "Bob's");
        await AddAsync(alice.Client, firstId);
        await AddAsync(alice.Client, secondId);
        await AddAsync(bob.Client, bobsId);

        var watchlist = await GetWatchlistAsync(alice.Client);

        Assert.Equal([secondId, firstId], watchlist!.Select(w => w.TmdbId));
    }

    [Fact]
    public async Task GetWatchlist_Empty_ReturnsEmptyList()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        Assert.Empty((await GetWatchlistAsync(user.Client))!);
    }

    // ---- Remove ----

    [Fact]
    public async Task RemoveFromWatchlist_RemovesOnlyThatMovie()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var keepId = _factory.Tmdb.AddMovie(9006, "Keep");
        var removeId = _factory.Tmdb.AddMovie(9007, "Remove");
        await AddAsync(user.Client, keepId);
        await AddAsync(user.Client, removeId);

        var response = await user.Client.DeleteAsync($"/api/watchlist/{removeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([keepId], (await GetWatchlistAsync(user.Client))!.Select(w => w.TmdbId));
    }

    [Fact]
    public async Task RemoveFromWatchlist_NotOnWatchlist_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync("/api/watchlist/9008");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveFromWatchlist_DoesNotAffectOtherUsers()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9009, "Shared");
        await AddAsync(alice.Client, tmdbId);
        await AddAsync(bob.Client, tmdbId);

        await bob.Client.DeleteAsync($"/api/watchlist/{tmdbId}");

        Assert.Single((await GetWatchlistAsync(alice.Client))!);
    }

    // ---- Watched ----

    [Fact]
    public async Task MarkWatched_RemovesMovieFromWatchlist()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var watchedId = _factory.Tmdb.AddMovie(9010, "Watched");
        var keepId = _factory.Tmdb.AddMovie(9011, "Still to watch");
        await AddAsync(user.Client, watchedId);
        await AddAsync(user.Client, keepId);

        var response = await user.Client.PostAsJsonAsync($"/api/movies/{watchedId}/status", new CreateStatusDto());

        response.EnsureSuccessStatusCode();
        Assert.Equal([keepId], (await GetWatchlistAsync(user.Client))!.Select(w => w.TmdbId));
    }

    [Fact]
    public async Task Rate_RemovesMovieFromWatchlist()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9012, "Rated");
        await AddAsync(user.Client, tmdbId);

        var response = await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 4 });

        response.EnsureSuccessStatusCode();
        Assert.Empty((await GetWatchlistAsync(user.Client))!);
    }

    [Fact]
    public async Task WatchedMovie_CanBeReaddedAndIsRemovedWhenLoggedAgain()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9013, "Rewatch");
        await AddAsync(user.Client, tmdbId);
        (await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 3 })).EnsureSuccessStatusCode();

        await AddAsync(user.Client, tmdbId);
        Assert.Single((await GetWatchlistAsync(user.Client))!);

        (await user.Client.PutAsJsonAsync($"/api/movies/{tmdbId}/status", new UpdateStatusDto { Rewatched = true })).EnsureSuccessStatusCode();
        Assert.Empty((await GetWatchlistAsync(user.Client))!);

        await AddAsync(user.Client, tmdbId);
        (await user.Client.PutAsJsonAsync($"/api/movies/{tmdbId}/rating", new UpdateRatingDto { Score = 4.5m })).EnsureSuccessStatusCode();
        Assert.Empty((await GetWatchlistAsync(user.Client))!);
    }

    [Fact]
    public async Task MarkWatched_DoesNotAffectOtherUsersWatchlist()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9014, "Shared watched");
        await AddAsync(alice.Client, tmdbId);
        await AddAsync(bob.Client, tmdbId);

        (await bob.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/status", new CreateStatusDto())).EnsureSuccessStatusCode();

        Assert.Single((await GetWatchlistAsync(alice.Client))!);
    }
}
