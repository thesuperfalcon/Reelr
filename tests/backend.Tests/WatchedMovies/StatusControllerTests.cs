using System.Net;
using System.Net.Http.Json;
using backend.Features.WatchedMovies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.WatchedMovies;

public class StatusControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public StatusControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static string StatusUrl(int tmdbId) => $"/api/movies/{tmdbId}/status";

    private static async Task MarkWatchedAsync(HttpClient client, int tmdbId, bool? liked = null, bool rewatched = false)
    {
        var response = await client.PostAsJsonAsync(StatusUrl(tmdbId), new CreateStatusDto { Liked = liked, Rewatched = rewatched });
        response.EnsureSuccessStatusCode();
    }

    private record WatchedMovieResponse(int TmdbId, string Title, string? PosterUrl, bool? Liked, bool Rewatched, DateTime WatchedAt);

    [Fact]
    public async Task Status_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var statusResponse = await client.GetAsync(StatusUrl(1));
        var watchedResponse = await client.GetAsync("/api/watched");

        Assert.Equal(HttpStatusCode.Unauthorized, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, watchedResponse.StatusCode);
    }

    // ---- Create ----

    [Fact]
    public async Task CreateStatus_NewMovie_StoresStatus()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8001, "Watched");

        var response = await user.Client.PostAsJsonAsync(StatusUrl(tmdbId), new CreateStatusDto { Liked = true, Rewatched = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await user.Client.GetFromJsonAsync<StatusDto>(StatusUrl(tmdbId));
        Assert.NotNull(status);
        Assert.Equal(tmdbId, status.TmdbId);
        Assert.True(status.Liked);
        Assert.False(status.Rewatched);
        Assert.InRange(status.WatchedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task CreateStatus_WithoutLiked_StoresNull()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8002, "Neutral");

        await MarkWatchedAsync(user.Client, tmdbId, liked: null, rewatched: true);

        var status = await user.Client.GetFromJsonAsync<StatusDto>(StatusUrl(tmdbId));
        Assert.Null(status!.Liked);
        Assert.True(status.Rewatched);
    }

    [Fact]
    public async Task CreateStatus_AlreadyExists_Returns409()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8003, "Twice");
        await MarkWatchedAsync(user.Client, tmdbId);

        var response = await user.Client.PostAsJsonAsync(StatusUrl(tmdbId), new CreateStatusDto());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatus_UnknownTmdbMovie_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync(StatusUrl(8999), new CreateStatusDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Read ----

    [Fact]
    public async Task GetStatus_NotWatched_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.GetAsync(StatusUrl(8004));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_OtherUsersStatus_Returns404()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8005, "Alice only");
        await MarkWatchedAsync(alice.Client, tmdbId);

        var response = await bob.Client.GetAsync(StatusUrl(tmdbId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWatchedMovies_ReturnsOwnMoviesNewestFirst()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var firstId = _factory.Tmdb.AddMovie(8006, "Older");
        var secondId = _factory.Tmdb.AddMovie(8007, "Newer");
        var bobsId = _factory.Tmdb.AddMovie(8008, "Bob's");
        await MarkWatchedAsync(alice.Client, firstId);
        await MarkWatchedAsync(alice.Client, secondId, liked: true);
        await MarkWatchedAsync(bob.Client, bobsId);

        var watched = await alice.Client.GetFromJsonAsync<List<WatchedMovieResponse>>("/api/watched");

        Assert.NotNull(watched);
        Assert.Equal([secondId, firstId], watched.Select(w => w.TmdbId));
        Assert.Equal("Newer", watched[0].Title);
        Assert.Equal("/8007.jpg", watched[0].PosterUrl);
        Assert.True(watched[0].Liked);
    }

    [Fact]
    public async Task GetWatchedMovies_NoneWatched_ReturnsEmptyList()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var watched = await user.Client.GetFromJsonAsync<List<WatchedMovieResponse>>("/api/watched");

        Assert.Empty(watched!);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateStatus_ChangesFlagsAndKeepsWatchedAt()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8009, "Changed mind");
        await MarkWatchedAsync(user.Client, tmdbId, liked: true);
        var before = await user.Client.GetFromJsonAsync<StatusDto>(StatusUrl(tmdbId));

        var response = await user.Client.PutAsJsonAsync(StatusUrl(tmdbId), new UpdateStatusDto { Liked = false, Rewatched = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await user.Client.GetFromJsonAsync<StatusDto>(StatusUrl(tmdbId));
        Assert.False(after!.Liked);
        Assert.True(after.Rewatched);
        Assert.Equal(before!.WatchedAt, after.WatchedAt);
    }

    [Fact]
    public async Task UpdateStatus_NotWatched_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync(StatusUrl(8010), new UpdateStatusDto { Liked = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_DoesNotAffectOtherUsers()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8011, "Shared");
        await MarkWatchedAsync(alice.Client, tmdbId, liked: true);
        await MarkWatchedAsync(bob.Client, tmdbId, liked: true);

        await bob.Client.PutAsJsonAsync(StatusUrl(tmdbId), new UpdateStatusDto { Liked = false });

        var aliceStatus = await alice.Client.GetFromJsonAsync<StatusDto>(StatusUrl(tmdbId));
        Assert.True(aliceStatus!.Liked);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteStatus_RemovesIt()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8012, "Unwatched");
        await MarkWatchedAsync(user.Client, tmdbId);

        var response = await user.Client.DeleteAsync(StatusUrl(tmdbId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await user.Client.GetAsync(StatusUrl(tmdbId));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteStatus_NotWatched_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync(StatusUrl(8013));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
