using System.Net;
using System.Net.Http.Json;
using backend.Features.Reviews;
using backend.Features.Reviews.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Reviews;

public class ReviewCommentControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public ReviewCommentControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<ReviewDto> CreateReviewAsync(TestUser author, int tmdbId)
    {
        var response = await author.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = "Discuss" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private static async Task<ReviewCommentDto> CommentAsync(TestUser user, int reviewId, string text)
    {
        var response = await user.Client.PostAsJsonAsync($"/api/reviews/{reviewId}/comments", new CreateReviewCommentDto { Text = text });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ReviewCommentDto>())!;
    }

    private Task<List<ReviewCommentDto>?> GetCommentsAsync(int reviewId) =>
        _factory.CreateClient().GetFromJsonAsync<List<ReviewCommentDto>>($"/api/reviews/{reviewId}/comments");

    [Fact]
    public async Task CreateComment_WithoutToken_Returns401()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6201, "Closed"));

        var response = await _factory.CreateClient().PostAsJsonAsync($"/api/reviews/{review.Id}/comments", new CreateReviewCommentDto { Text = "Hi" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_TrimsText_AndListsOldestFirst()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6202, "Thread"));

        var first = await CommentAsync(reader, review.Id, "  First!  ");
        await CommentAsync(author, review.Id, "Thanks");

        Assert.Equal("First!", first.Text);
        Assert.Equal(reader.Username, first.Username);
        var comments = await GetCommentsAsync(review.Id);
        Assert.Equal(["First!", "Thanks"], comments!.Select(c => c.Text));
        var counted = await _factory.CreateClient().GetFromJsonAsync<ReviewDto>($"/api/reviews/{review.Id}");
        Assert.Equal(2, counted!.CommentCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateComment_BlankText_Returns400(string text)
    {
        var reader = await _factory.CreateAuthenticatedAsync();
        var author = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(text.Length == 0 ? 6203 : 6204, "Silent"));

        var response = await reader.Client.PostAsJsonAsync($"/api/reviews/{review.Id}/comments", new CreateReviewCommentDto { Text = text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_TooLong_Returns400()
    {
        var reader = await _factory.CreateAuthenticatedAsync();
        var author = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6205, "Rambling"));

        var response = await reader.Client.PostAsJsonAsync($"/api/reviews/{review.Id}/comments",
            new CreateReviewCommentDto { Text = new string('a', ReviewComment.MaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_UnknownReview_Returns404()
    {
        var reader = await _factory.CreateAuthenticatedAsync();

        var response = await reader.Client.PostAsJsonAsync("/api/reviews/999999/comments", new CreateReviewCommentDto { Text = "Hello?" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_Author_ChangesTextAndMarksEdited()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6206, "Typo"));
        var comment = await CommentAsync(reader, review.Id, "Grate film");

        var response = await reader.Client.PutAsJsonAsync($"/api/comments/{comment.Id}", new UpdateReviewCommentDto { Text = "Great film" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ReviewCommentDto>();
        Assert.Equal("Great film", updated!.Text);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateComment_ReviewAuthor_Returns403()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6207, "Not yours"));
        var comment = await CommentAsync(reader, review.Id, "Mine");

        var response = await author.Client.PutAsJsonAsync($"/api/comments/{comment.Id}", new UpdateReviewCommentDto { Text = "Rewritten" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_ByCommentAuthorOrReviewAuthor_RemovesIt()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6208, "Moderated"));
        var own = await CommentAsync(reader, review.Id, "Oops");
        var rude = await CommentAsync(reader, review.Id, "Rude");

        var byWriter = await reader.Client.DeleteAsync($"/api/comments/{own.Id}");
        var byReviewAuthor = await author.Client.DeleteAsync($"/api/comments/{rude.Id}");

        Assert.Equal(HttpStatusCode.NoContent, byWriter.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byReviewAuthor.StatusCode);
        Assert.Empty((await GetCommentsAsync(review.Id))!);
    }

    [Fact]
    public async Task DeleteComment_ByStranger_Returns403AndKeepsIt()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var stranger = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6209, "Kept"));
        var comment = await CommentAsync(reader, review.Id, "Stays");

        var response = await stranger.Client.DeleteAsync($"/api/comments/{comment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single((await GetCommentsAsync(review.Id))!);
    }

    [Fact]
    public async Task DeleteReview_RemovesItsComments()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reader = await _factory.CreateAuthenticatedAsync();
        var review = await CreateReviewAsync(author, _factory.Tmdb.AddMovie(6210, "Gone"));
        await CommentAsync(reader, review.Id, "Bye");

        (await author.Client.DeleteAsync($"/api/reviews/{review.Id}")).EnsureSuccessStatusCode();

        var response = await _factory.CreateClient().GetAsync($"/api/reviews/{review.Id}/comments");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await _factory.WithContextAsync(context => Task.FromResult(context.ReviewComments.Count(c => c.ReviewId == review.Id))));
    }
}
