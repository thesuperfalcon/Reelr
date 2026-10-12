using System.Net;
using System.Net.Http.Json;
using backend.Features.Activity.DTOs;
using backend.Features.Reviews.DTOs;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Users;

public class BlockTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public BlockTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task BlockAsync(TestUser blocker, TestUser blocked) =>
        Assert.Equal(HttpStatusCode.NoContent, (await blocker.Client.PutAsync($"/api/users/{blocked.Id}/block", null)).StatusCode);

    private static async Task<ReviewDto> ReviewAsync(TestUser author, int tmdbId)
    {
        var response = await author.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = $"Review by {author.Username}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private static async Task<UserProfileDto> ProfileAsync(HttpClient client, TestUser user) =>
        (await client.GetFromJsonAsync<UserProfileDto>($"/api/users/{user.Id}"))!;

    [Fact]
    public async Task Block_EndsFollowsBothWays_AndNeitherCanFollowAgain()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        (await alice.Client.PostAsync($"/api/users/{bob.Id}/follow", null)).EnsureSuccessStatusCode();
        (await bob.Client.PostAsync($"/api/users/{alice.Id}/follow", null)).EnsureSuccessStatusCode();

        await BlockAsync(alice, bob);

        var aliceProfile = await ProfileAsync(bob.Client, alice);
        Assert.Equal(0, aliceProfile.FollowerCount);
        Assert.Equal(0, aliceProfile.FollowingCount);
        Assert.True((await ProfileAsync(alice.Client, bob)).IsBlocked);
        Assert.False((await ProfileAsync(bob.Client, alice)).IsBlocked);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.Client.PostAsync($"/api/users/{alice.Id}/follow", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PostAsync($"/api/users/{bob.Id}/follow", null)).StatusCode);
    }

    [Fact]
    public async Task BlockedUser_CannotCommentOnOrLikeTheBlockersReviews()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var review = await ReviewAsync(alice, _factory.Tmdb.AddMovie(9601, "Blocked film"));

        await BlockAsync(alice, bob);

        var comment = await bob.Client.PostAsJsonAsync($"/api/reviews/{review.Id}/comments", new CreateReviewCommentDto { Text = "Hi" });
        var like = await bob.Client.PutAsync($"/api/reviews/{review.Id}/like", null);
        Assert.Equal(HttpStatusCode.Forbidden, comment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, like.StatusCode);
    }

    [Fact]
    public async Task Blocker_NoLongerSeesTheBlockedUsersReviewsCommentsOrCommunityActivity_UntilUnblocking()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var carol = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9602, "Shared film");
        await ReviewAsync(bob, tmdbId);
        var carolsReview = await ReviewAsync(carol, tmdbId);
        (await bob.Client.PostAsJsonAsync($"/api/reviews/{carolsReview.Id}/comments", new CreateReviewCommentDto { Text = "From Bob" })).EnsureSuccessStatusCode();

        await BlockAsync(alice, bob);

        async Task<bool> SeesBob()
        {
            var reviews = (await alice.Client.GetItemsAsync<ReviewDto>($"/api/movies/{tmdbId}/reviews"))!;
            var comments = (await alice.Client.GetItemsAsync<ReviewCommentDto>($"/api/reviews/{carolsReview.Id}/comments"))!;
            var feed = (await alice.Client.GetFromJsonAsync<ActivityPageDto>("/api/feed/community"))!;
            var seen = new[]
            {
                reviews.Any(r => r.UserId == bob.Id),
                comments.Any(c => c.UserId == bob.Id),
                feed.Items.Any(i => i.Actor.Id == bob.Id)
            };
            Assert.True(seen.All(s => s == seen[0]), $"Mixed visibility: {string.Join(", ", seen)}");
            return seen[0];
        }

        Assert.False(await SeesBob());
        // Others still see Bob.
        Assert.Contains((await carol.Client.GetItemsAsync<ReviewDto>($"/api/movies/{tmdbId}/reviews"))!, r => r.UserId == bob.Id);

        Assert.Single((await alice.Client.GetFromJsonAsync<List<UserSummaryDto>>("/api/settings/blocked"))!, u => u.Id == bob.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.Client.DeleteAsync($"/api/users/{bob.Id}/block")).StatusCode);

        Assert.True(await SeesBob());
        Assert.Empty((await alice.Client.GetFromJsonAsync<List<UserSummaryDto>>("/api/settings/blocked"))!);
    }

    [Fact]
    public async Task Block_Self_Returns400_AndUnknownUser_Returns404()
    {
        var alice = await _factory.CreateAuthenticatedAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Client.PutAsync($"/api/users/{alice.Id}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Client.PutAsync("/api/users/999999/block", null)).StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithBlocksBothWays_Works()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await BlockAsync(alice, bob);
        await BlockAsync(bob, alice);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.Client.DeleteAccountAsync(alice.Id)).StatusCode);
        Assert.Empty((await bob.Client.GetFromJsonAsync<List<UserSummaryDto>>("/api/settings/blocked"))!);
    }
}
