using System.Net;
using System.Net.Http.Json;
using backend.Features.Ratings.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Ratings;

public class RatingControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public RatingControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static string RatingUrl(int tmdbId) => $"/api/movies/{tmdbId}/rating";

    private static async Task RateAsync(HttpClient client, int tmdbId, decimal score)
    {
        var response = await client.PostAsJsonAsync(RatingUrl(tmdbId), new CreateRatingDto { Score = score });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Rating_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync(RatingUrl(1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Create ----

    [Fact]
    public async Task CreateRating_NewMovie_StoresScore()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5001, "Rated");

        var response = await user.Client.PostAsJsonAsync(RatingUrl(tmdbId), new CreateRatingDto { Score = 4.5m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await user.Client.GetFromJsonAsync<RatingDto>(RatingUrl(tmdbId));
        Assert.Equal(tmdbId, stored!.TmdbId);
        Assert.Equal(4.5m, stored.Score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(5)]
    public async Task CreateRating_BoundaryScores_AreAccepted(decimal score)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5002, "Boundary");

        var response = await user.Client.PostAsJsonAsync(RatingUrl(tmdbId), new CreateRatingDto { Score = score });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(5.5)]
    [InlineData(2.3)]
    [InlineData(3.25)]
    public async Task CreateRating_InvalidScore_Returns400(decimal score)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5003, "Invalid");

        var response = await user.Client.PostAsJsonAsync(RatingUrl(tmdbId), new CreateRatingDto { Score = score });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRating_AlreadyRated_Returns409()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5004, "Twice");
        await RateAsync(user.Client, tmdbId, 3);

        var response = await user.Client.PostAsJsonAsync(RatingUrl(tmdbId), new CreateRatingDto { Score = 4 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateRating_UnknownTmdbMovie_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync(RatingUrl(5999), new CreateRatingDto { Score = 3 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Read ----

    [Fact]
    public async Task GetRating_NotRated_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.GetAsync(RatingUrl(5005));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRating_ReturnsOnlyCurrentUsersRating()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5006, "Shared");
        await RateAsync(alice.Client, tmdbId, 5);

        var response = await bob.Client.GetAsync(RatingUrl(tmdbId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateRating_ChangesScore()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5007, "Changed");
        await RateAsync(user.Client, tmdbId, 2);

        var response = await user.Client.PutAsJsonAsync(RatingUrl(tmdbId), new UpdateRatingDto { Score = 3.5m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await user.Client.GetFromJsonAsync<RatingDto>(RatingUrl(tmdbId));
        Assert.Equal(3.5m, stored!.Score);
    }

    [Fact]
    public async Task UpdateRating_InvalidScore_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5008, "Bad update");
        await RateAsync(user.Client, tmdbId, 2);

        var response = await user.Client.PutAsJsonAsync(RatingUrl(tmdbId), new UpdateRatingDto { Score = 2.7m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRating_NotRated_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync(RatingUrl(5009), new UpdateRatingDto { Score = 3 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRating_DoesNotAffectOtherUsers()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5010, "Independent");
        await RateAsync(alice.Client, tmdbId, 1);
        await RateAsync(bob.Client, tmdbId, 2);

        await bob.Client.PutAsJsonAsync(RatingUrl(tmdbId), new UpdateRatingDto { Score = 5 });

        var aliceRating = await alice.Client.GetFromJsonAsync<RatingDto>(RatingUrl(tmdbId));
        Assert.Equal(1m, aliceRating!.Score);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteRating_RemovesIt()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(5011, "Removed");
        await RateAsync(user.Client, tmdbId, 4);

        var response = await user.Client.DeleteAsync(RatingUrl(tmdbId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await user.Client.GetAsync(RatingUrl(tmdbId));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteRating_NotRated_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync(RatingUrl(5012));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
