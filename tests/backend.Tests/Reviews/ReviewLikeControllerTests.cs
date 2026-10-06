using System.Net;
using System.Net.Http.Json;
using backend.Features.Reviews.DTOs;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Reviews;

public class ReviewLikeControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public ReviewLikeControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<ReviewDto> CreateReviewAsync(TestUser author, int tmdbId)
    {
        var response = await author.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = "Worth a look" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private static async Task<ReviewLikeStateDto> LikeAsync(TestUser user, int reviewId)
    {
        var response = await user.Client.PutAsync($"/api/reviews/{reviewId}/like", null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReviewLikeStateDto>())!;
    }

    [Fact]
    public async Task Like_WithoutToken_Returns401()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6101, "Anonymous"));

        var response = await _factory.CreateClient().PutAsync($"/api/reviews/{review.Id}/like", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Like_Twice_CountsOnce()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var fan = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6102, "Liked"));

        await LikeAsync(fan, review.Id);
        var state = await LikeAsync(fan, review.Id);

        Assert.Equal(1, state.LikeCount);
        Assert.True(state.LikedByMe);
    }

    [Fact]
    public async Task Like_OwnReview_Returns400()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6103, "Self love"));

        var response = await author.Client.PutAsync($"/api/reviews/{review.Id}/like", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Like_UnknownReview_Returns404()
    {
        var fan = await _factory.CreateAuthenticatedAsync();

        var response = await fan.Client.PutAsync("/api/reviews/999999/like", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unlike_RemovesLike_AndDoesNothingWhenNotLiked()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var fan = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6104, "Changed mind"));
        await LikeAsync(fan, review.Id);

        var first = await fan.Client.DeleteAsync($"/api/reviews/{review.Id}/like");
        var second = await fan.Client.DeleteAsync($"/api/reviews/{review.Id}/like");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var state = await second.Content.ReadFromJsonAsync<ReviewLikeStateDto>();
        Assert.Equal(0, state!.LikeCount);
        Assert.False(state.LikedByMe);
    }

    [Fact]
    public async Task GetReview_ShowsLikeCount_AndLikedByMeOnlyForTheLiker()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var fan = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6105, "Popular"));
        await LikeAsync(fan, review.Id);
        await LikeAsync(other, review.Id);
        (await other.Client.DeleteAsync($"/api/reviews/{review.Id}/like")).EnsureSuccessStatusCode();

        var asFan = await fan.Client.GetFromJsonAsync<ReviewDto>($"/api/reviews/{review.Id}");
        var asOther = await other.Client.GetFromJsonAsync<ReviewDto>($"/api/reviews/{review.Id}");
        var anonymous = await _factory.CreateClient().GetFromJsonAsync<ReviewDto>($"/api/reviews/{review.Id}");

        Assert.Equal(1, asFan!.LikeCount);
        Assert.True(asFan.LikedByMe);
        Assert.False(asOther!.LikedByMe);
        Assert.Equal(1, anonymous!.LikeCount);
        Assert.False(anonymous.LikedByMe);
    }

    [Fact]
    public async Task GetLikes_ListsLikers()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var fan = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6106, "Who liked"));
        await LikeAsync(fan, review.Id);

        var likers = await _factory.CreateClient().GetFromJsonAsync<List<UserSummaryDto>>($"/api/reviews/{review.Id}/likes");

        Assert.Equal(fan.Username, Assert.Single(likers!).UserName);
    }

    [Fact]
    public async Task DeleteReview_RemovesItsLikes()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var fan = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6107, "Gone"));
        await LikeAsync(fan, review.Id);

        (await author.Client.DeleteAsync($"/api/reviews/{review.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(0, await _factory.WithContextAsync(context => Task.FromResult(context.ReviewLikes.Count(l => l.ReviewId == review.Id))));
    }
}
