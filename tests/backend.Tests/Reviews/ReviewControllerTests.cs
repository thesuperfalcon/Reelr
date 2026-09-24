using System.Net;
using System.Net.Http.Json;
using backend.Features.Ratings.DTOs;
using backend.Features.Reviews.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Reviews;

public class ReviewControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public ReviewControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static string ReviewsUrl(int tmdbId) => $"/api/movies/{tmdbId}/reviews";

    private static async Task<ReviewDto> CreateReviewAsync(HttpClient client, int tmdbId, string text)
    {
        var response = await client.PostAsJsonAsync(ReviewsUrl(tmdbId), new CreateReviewDto { Text = text });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    // ---- Create ----

    [Fact]
    public async Task CreateReview_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(ReviewsUrl(6000), new CreateReviewDto { Text = "Hi" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_ReturnsReviewWithoutScoreWhenNotRated()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6001, "Reviewed");

        var response = await user.Client.PostAsJsonAsync(ReviewsUrl(tmdbId), new CreateReviewDto { Text = "Great film" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(review);
        Assert.Equal(user.Id, review.UserId);
        Assert.Equal(user.Username, review.Username);
        Assert.Equal(tmdbId, review.TmdbId);
        Assert.Equal("Great film", review.Text);
        Assert.Null(review.Score);
        Assert.Null(review.UpdatedAt);
    }

    [Fact]
    public async Task CreateReview_IncludesUsersRating()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6002, "Rated and reviewed");
        await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 4.5m });

        var review = await CreateReviewAsync(user.Client, tmdbId, "Loved it");

        Assert.Equal(4.5m, review.Score);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateReview_WithoutText_Returns400(string? text)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6003, "Empty");

        var response = await user.Client.PostAsJsonAsync(ReviewsUrl(tmdbId), new { Text = text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_AlreadyReviewed_Returns409()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6004, "Twice");
        await CreateReviewAsync(user.Client, tmdbId, "First");

        var response = await user.Client.PostAsJsonAsync(ReviewsUrl(tmdbId), new CreateReviewDto { Text = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_UnknownTmdbMovie_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync(ReviewsUrl(6999), new CreateReviewDto { Text = "Ghost" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Read ----

    [Fact]
    public async Task GetReviews_IsPublicAndReturnsNewestFirstWithEachUsersScore()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6005, "Discussed");
        await alice.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 2 });
        await CreateReviewAsync(alice.Client, tmdbId, "Meh");
        await CreateReviewAsync(bob.Client, tmdbId, "Masterpiece");

        var reviews = await _factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>(ReviewsUrl(tmdbId));

        Assert.NotNull(reviews);
        Assert.Equal(["Masterpiece", "Meh"], reviews.Select(r => r.Text));
        Assert.Null(reviews[0].Score);
        Assert.Equal(2m, reviews[1].Score);
    }

    [Fact]
    public async Task GetReviews_NoReviews_ReturnsEmptyList()
    {
        var reviews = await _factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>(ReviewsUrl(6006));

        Assert.NotNull(reviews);
        Assert.Empty(reviews);
    }

    [Fact]
    public async Task GetReviewsByUser_ReturnsOnlyThatUsersReviews()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var firstId = _factory.Tmdb.AddMovie(6007, "One");
        var secondId = _factory.Tmdb.AddMovie(6008, "Two");
        await CreateReviewAsync(alice.Client, firstId, "Alice on one");
        await CreateReviewAsync(alice.Client, secondId, "Alice on two");
        await CreateReviewAsync(bob.Client, firstId, "Bob on one");

        var reviews = await _factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>($"/api/users/{alice.Id}/reviews");

        Assert.NotNull(reviews);
        Assert.Equal(["Alice on two", "Alice on one"], reviews.Select(r => r.Text));
        Assert.Equal([secondId, firstId], reviews.Select(r => r.TmdbId));
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateReview_Owner_ChangesText()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6009, "Edited");
        var review = await CreateReviewAsync(user.Client, tmdbId, "Draft");

        var response = await user.Client.PutAsJsonAsync($"/api/reviews/{review.Id}", new UpdateReviewDto { Text = "Final" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.Equal("Final", updated!.Text);
        Assert.Equal(tmdbId, updated.TmdbId);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateReview_OtherUser_Returns404AndKeepsText()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6010, "Protected");
        var review = await CreateReviewAsync(owner.Client, tmdbId, "Original");

        var response = await other.Client.PutAsJsonAsync($"/api/reviews/{review.Id}", new UpdateReviewDto { Text = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var reviews = await owner.Client.GetFromJsonAsync<List<ReviewDto>>(ReviewsUrl(tmdbId));
        Assert.Equal("Original", Assert.Single(reviews!).Text);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteReview_Owner_RemovesIt()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6011, "Deleted");
        var review = await CreateReviewAsync(user.Client, tmdbId, "Gone soon");

        var response = await user.Client.DeleteAsync($"/api/reviews/{review.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reviews = await user.Client.GetFromJsonAsync<List<ReviewDto>>(ReviewsUrl(tmdbId));
        Assert.Empty(reviews!);
    }

    [Fact]
    public async Task DeleteReview_OtherUser_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6012, "Kept");
        var review = await CreateReviewAsync(owner.Client, tmdbId, "Mine");

        var response = await other.Client.DeleteAsync($"/api/reviews/{review.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var reviews = await owner.Client.GetFromJsonAsync<List<ReviewDto>>(ReviewsUrl(tmdbId));
        Assert.Single(reviews!);
    }
}
