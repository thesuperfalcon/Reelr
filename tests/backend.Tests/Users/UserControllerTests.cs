using System.Net;
using System.Net.Http.Json;
using backend.Features.MovieLists.DTOs;
using backend.Features.Ratings.DTOs;
using backend.Features.Reviews.DTOs;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Users;

public class UserControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public UserControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private Task<UserProfileDto?> GetProfileAsync(int userId) =>
        _factory.CreateClient().GetFromJsonAsync<UserProfileDto>($"/api/users/{userId}");

    private static async Task FollowAsync(TestUser follower, TestUser followed)
    {
        var response = await follower.Client.PostAsync($"/api/users/{followed.Id}/follow", null);
        response.EnsureSuccessStatusCode();
    }

    // ---- Get ----

    [Fact]
    public async Task GetUser_IsPublicAndReturnsProfile()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var profile = await GetProfileAsync(user.Id);

        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.Id);
        Assert.Equal(user.Username, profile.UserName);
        Assert.Null(profile.ProfileImageUrl);
        Assert.Equal(0, profile.FollowerCount);
        Assert.Equal(0, profile.FollowingCount);
    }

    [Fact]
    public async Task GetUser_UnknownId_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateUser_WithoutToken_Returns401()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await _factory.CreateClient().PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserDto { Username = "anonymous" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_ChangesUsernameAndProfileImage()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var newName = $"renamed_{Guid.NewGuid():N}"[..20];

        var response = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserDto
        {
            Username = newName,
            ProfileImageUrl = "https://img.test/me.png"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await GetProfileAsync(user.Id);
        Assert.Equal(newName, profile!.UserName);
        Assert.Equal("https://img.test/me.png", profile.ProfileImageUrl);
    }

    [Fact]
    public async Task UpdateUser_OnlyProfileImage_KeepsUsername()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserDto { ProfileImageUrl = "https://img.test/new.png" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await GetProfileAsync(user.Id);
        Assert.Equal(user.Username, profile!.UserName);
        Assert.Equal("https://img.test/new.png", profile.ProfileImageUrl);
    }

    [Fact]
    public async Task UpdateUser_UsernameTakenByOtherUser_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserDto { Username = other.Username });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var profile = await GetProfileAsync(user.Id);
        Assert.Equal(user.Username, profile!.UserName);
    }

    [Fact]
    public async Task UpdateUser_TooShortUsername_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserDto { Username = "ab" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_OtherUser_Returns403AndKeepsProfile()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var victim = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PutAsJsonAsync($"/api/users/{victim.Id}", new UpdateUserDto { Username = "hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var profile = await GetProfileAsync(victim.Id);
        Assert.Equal(victim.Username, profile!.UserName);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteUser_Own_RemovesAccount()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await _factory.CreateClient().GetAsync($"/api/users/{user.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        var loginResponse = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { UserInput = user.Username, TestUsers.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithContent_RemovesAccount()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7001, "Left behind");
        await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/rating", new CreateRatingDto { Score = 3 });
        await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = "Bye" });
        await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null);
        await user.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "Last list" });

        var response = await user.Client.DeleteAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reviews = await _factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>($"/api/movies/{tmdbId}/reviews");
        Assert.DoesNotContain(reviews!, r => r.UserId == user.Id);
    }

    [Fact]
    public async Task DeleteUser_WithFollowers_RemovesAccountAndFollows()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var follower = await _factory.CreateAuthenticatedAsync();
        var followed = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(follower, user);
        await FollowAsync(user, followed);

        var response = await user.Client.DeleteAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await GetProfileAsync(follower.Id))!.FollowingCount);
        Assert.Equal(0, (await GetProfileAsync(followed.Id))!.FollowerCount);
    }

    [Fact]
    public async Task DeleteUser_OtherUser_Returns403AndKeepsAccount()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var victim = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync($"/api/users/{victim.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await GetProfileAsync(victim.Id));
    }

    // ---- Follow ----

    [Fact]
    public async Task FollowUser_UpdatesCountsOnBothProfiles()
    {
        var follower = await _factory.CreateAuthenticatedAsync();
        var followed = await _factory.CreateAuthenticatedAsync();

        var response = await follower.Client.PostAsync($"/api/users/{followed.Id}/follow", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, (await GetProfileAsync(follower.Id))!.FollowingCount);
        Assert.Equal(1, (await GetProfileAsync(followed.Id))!.FollowerCount);
    }

    [Fact]
    public async Task FollowUser_WithoutToken_Returns401()
    {
        var followed = await _factory.CreateAuthenticatedAsync();

        var response = await _factory.CreateClient().PostAsync($"/api/users/{followed.Id}/follow", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FollowUser_Self_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsync($"/api/users/{user.Id}/follow", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FollowUser_UnknownUser_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsync("/api/users/999999/follow", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FollowUser_AlreadyFollowing_Returns409()
    {
        var follower = await _factory.CreateAuthenticatedAsync();
        var followed = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(follower, followed);

        var response = await follower.Client.PostAsync($"/api/users/{followed.Id}/follow", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Unfollow ----

    [Fact]
    public async Task UnfollowUser_RemovesFollow()
    {
        var follower = await _factory.CreateAuthenticatedAsync();
        var followed = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(follower, followed);

        var response = await follower.Client.DeleteAsync($"/api/users/{followed.Id}/follow");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await GetProfileAsync(followed.Id))!.FollowerCount);
    }

    [Fact]
    public async Task UnfollowUser_NotFollowing_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.DeleteAsync($"/api/users/{other.Id}/follow");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnfollowUser_OnlyRemovesOwnFollow()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var target = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, target);
        await FollowAsync(bob, target);

        await alice.Client.DeleteAsync($"/api/users/{target.Id}/follow");

        Assert.Equal(1, (await GetProfileAsync(target.Id))!.FollowerCount);
        Assert.Equal(1, (await GetProfileAsync(bob.Id))!.FollowingCount);
    }
}
