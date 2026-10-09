using System.Net;
using System.Net.Http.Json;
using backend.Features.MovieLists.DTOs;
using backend.Features.Ratings.DTOs;
using backend.Features.Reviews.DTOs;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Infrastructure;

/// <summary>
/// The same request sent several times at once, as a double click or a client retry does. Only one may
/// succeed; the others must get 409, never a 500 from the database refusing a duplicate.
/// </summary>
public class ConcurrencyTests : IClassFixture<ReelrApiFactory>
{
    private const int Parallel = 6;

    private readonly ReelrApiFactory _factory;

    public ConcurrencyTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpStatusCode[]> SendAtOnceAsync(Func<Task<HttpResponseMessage>> send)
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, Parallel).Select(_ => send()));
        return responses.Select(r => r.StatusCode).ToArray();
    }

    private static void AssertOneSucceeded(HttpStatusCode[] statuses, HttpStatusCode success)
    {
        Assert.All(statuses, status => Assert.True(status == success || status == HttpStatusCode.Conflict, $"Got {status}"));
        Assert.Single(statuses, status => status == success);
    }

    [Fact]
    public async Task Follow_SentAtOnce_FollowsOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();

        var statuses = await SendAtOnceAsync(() => user.Client.PostAsync($"/api/users/{other.Id}/follow", null));

        AssertOneSucceeded(statuses, HttpStatusCode.NoContent);
        Assert.Equal(1, await _factory.WithContextAsync(c => c.Follows.CountAsync(f => f.FollowerId == user.Id)));
    }

    [Fact]
    public async Task Rating_SentAtOnce_RatesOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8101, "Rated at once");

        var statuses = await SendAtOnceAsync(() =>
            user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 4 }));

        AssertOneSucceeded(statuses, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Watchlist_SentAtOnce_AddsOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8102, "Watchlisted at once");

        var statuses = await SendAtOnceAsync(() => user.Client.PostAsync($"/api/watchlist/{tmdbId}", null));

        AssertOneSucceeded(statuses, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Review_SentAtOnce_ReviewsOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8103, "Reviewed at once");

        var statuses = await SendAtOnceAsync(() =>
            user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = "Twice?" }));

        AssertOneSucceeded(statuses, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListItem_SentAtOnce_AddsOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8104, "Listed at once");
        var list = await (await user.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "At once" }))
            .Content.ReadFromJsonAsync<MovieListDto>();

        var statuses = await SendAtOnceAsync(() =>
            user.Client.PostAsJsonAsync($"/api/lists/{list!.Id}/movies", new AddMovieToListDto { TmdbId = tmdbId }));

        AssertOneSucceeded(statuses, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FirstLog_SentAtOnce_NeverFailsWithServerError()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(8105, "Logged at once");

        // Logging the same film again is allowed (a rewatch), so several may succeed. A clash on the
        // film's first watched status must still be a 409, not a 500.
        var statuses = await SendAtOnceAsync(() =>
            user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new { Score = 4 }));

        Assert.All(statuses, status => Assert.True(status is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Got {status}"));
        Assert.Contains(HttpStatusCode.OK, statuses);
    }
}
