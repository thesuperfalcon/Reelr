using System.Net;
using System.Net.Http.Json;
using backend.Features.Activity;
using backend.Features.Activity.DTOs;
using backend.Features.Diary.DTOs;
using backend.Features.Reviews.DTOs;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Users;

/// <summary>
/// Who sees a user's diary: on their profile, in the Following and Community feeds, on the "New from friends" shelf,
/// and as the rating and watch date on their reviews.
/// </summary>
public class DiaryVisibilityTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public DiaryVisibilityTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task SetDiaryVisibilityAsync(TestUser user, DiaryVisibility visibility) =>
        (await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { DiaryVisibility = visibility }))
            .EnsureSuccessStatusCode();

    private static async Task LogAsync(TestUser user, int tmdbId) =>
        (await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto { Score = 4 })).EnsureSuccessStatusCode();

    private static async Task FollowAsync(TestUser follower, TestUser followed) =>
        (await follower.Client.PostAsync($"/api/users/{followed.Id}/follow", null)).EnsureSuccessStatusCode();

    private static async Task<List<ActivityItemDto>> WatchedByAsync(HttpClient client, string feed, TestUser actor)
    {
        var page = (await client.GetFromJsonAsync<ActivityPageDto>($"/api/feed/{feed}"))!;
        return page.Items.Where(i => i.Actor.Id == actor.Id && i.Type == ActivityTypes.Watched).ToList();
    }

    [Theory]
    [InlineData(DiaryVisibility.Public, true, true, true)]
    [InlineData(DiaryVisibility.Followers, true, false, false)]
    [InlineData(DiaryVisibility.Private, false, false, false)]
    public async Task GetUserDiary_FollowsVisibility(DiaryVisibility visibility, bool followerSees, bool strangerSees, bool anonymousSees)
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var follower = await _factory.CreateAuthenticatedAsync();
        var stranger = await _factory.CreateAuthenticatedAsync();
        await LogAsync(owner, _factory.Tmdb.AddMovie(9301 + (int)visibility, "Logged"));
        await FollowAsync(follower, owner);
        await SetDiaryVisibilityAsync(owner, visibility);
        var url = $"/api/users/{owner.Id}/diary";

        async Task<bool> Sees(HttpClient client)
        {
            var response = await client.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            Assert.Single((await response.Content.ReadItemsAsync<DiaryEntryDto>())!);
            return true;
        }

        Assert.True(await Sees(owner.Client));
        Assert.Equal(followerSees, await Sees(follower.Client));
        Assert.Equal(strangerSees, await Sees(stranger.Client));
        Assert.Equal(anonymousSees, await Sees(_factory.CreateClient()));

        var profile = await stranger.Client.GetFromJsonAsync<UserProfileDto>($"/api/users/{owner.Id}");
        Assert.Equal(visibility, profile!.DiaryVisibility);
        Assert.Equal(strangerSees, profile.CanSeeDiary);
    }

    [Fact]
    public async Task NewUser_DiaryIsPublic()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var settings = await user.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");

        Assert.Equal(DiaryVisibility.Public, settings!.DiaryVisibility);
    }

    [Fact]
    public async Task PrivateDiary_StaysOutOfFollowingFeedAndFriendsShelf_ButOwnerStillSeesIt()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await LogAsync(bob, _factory.Tmdb.AddMovie(9311, "Secret watch"));
        await SetDiaryVisibilityAsync(bob, DiaryVisibility.Private);

        Assert.Empty(await WatchedByAsync(alice.Client, "following", bob));
        var shelf = (await alice.Client.GetFromJsonAsync<List<FollowingFilmDto>>("/api/feed/following/films"))!;
        Assert.DoesNotContain(shelf, f => f.Watchers.Any(w => w.Actor.Id == bob.Id));
        Assert.Single(await WatchedByAsync(bob.Client, "following", bob));
    }

    [Fact]
    public async Task FollowersOnlyDiary_ShowsToFollowers_ButNotInCommunityFeed()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await LogAsync(bob, _factory.Tmdb.AddMovie(9312, "Friends only"));
        await SetDiaryVisibilityAsync(bob, DiaryVisibility.Followers);

        Assert.Single(await WatchedByAsync(alice.Client, "following", bob));
        Assert.Empty(await WatchedByAsync(alice.Client, "community", bob));
        Assert.Empty(await WatchedByAsync(_factory.CreateClient(), "community", bob));
    }

    [Theory]
    [InlineData(DiaryVisibility.Public, true, true, true)]
    [InlineData(DiaryVisibility.Followers, true, false, false)]
    [InlineData(DiaryVisibility.Private, false, false, false)]
    public async Task Review_ShowsRatingAndWatchDate_OnlyToWhoMaySeeTheDiary(
        DiaryVisibility visibility, bool followerSees, bool strangerSees, bool anonymousSees)
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var follower = await _factory.CreateAuthenticatedAsync();
        var stranger = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9321 + (int)visibility, "Reviewed");
        (await owner.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto { Score = 4, Review = "Loved it" }))
            .EnsureSuccessStatusCode();
        await FollowAsync(follower, owner);
        await SetDiaryVisibilityAsync(owner, visibility);
        var page = (await _factory.CreateClient().GetFromJsonAsync<MovieReviewPageDto>($"/api/movies/{tmdbId}/reviews"))!;
        var reviewId = Assert.Single(page.Items).Id;

        // The review itself stays public; only the diary data on it follows the setting.
        async Task<bool> Sees(HttpClient client)
        {
            var review = (await client.GetFromJsonAsync<ReviewDto>($"/api/reviews/{reviewId}"))!;
            Assert.Equal("Loved it", review.Text);
            Assert.Equal(review.Score != null, review.WatchedAt != null);
            return review.Score != null;
        }

        Assert.True(await Sees(owner.Client));
        Assert.Equal(followerSees, await Sees(follower.Client));
        Assert.Equal(strangerSees, await Sees(stranger.Client));
        Assert.Equal(anonymousSees, await Sees(_factory.CreateClient()));

        var byUser = await (await stranger.Client.GetAsync($"/api/users/{owner.Id}/reviews")).Content.ReadItemsAsync<ReviewDto>();
        Assert.Equal(strangerSees, Assert.Single(byUser).Score != null);
    }

    [Theory]
    [InlineData(DiaryVisibility.Followers, true)]
    [InlineData(DiaryVisibility.Private, false)]
    public async Task ReviewInFollowingFeed_ShowsDiaryData_OnlyWhenTheDiaryIsVisible(DiaryVisibility visibility, bool followerSees)
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var tmdbId = _factory.Tmdb.AddMovie(9331 + (int)visibility, "Reviewed in the feed");
        (await bob.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto { Score = 4, Liked = true, Review = "Great" }))
            .EnsureSuccessStatusCode();
        await SetDiaryVisibilityAsync(bob, visibility);

        async Task<ActivityItemDto> ReviewedBy(HttpClient client)
        {
            var feed = (await client.GetFromJsonAsync<ActivityPageDto>("/api/feed/following"))!;
            return Assert.Single(feed.Items, i => i.Actor.Id == bob.Id && i.Type == ActivityTypes.Reviewed);
        }

        var seenByAlice = await ReviewedBy(alice.Client);
        Assert.NotNull(seenByAlice.Review);
        Assert.Equal(followerSees, seenByAlice.Rating != null);
        Assert.Equal(followerSees, seenByAlice.Liked != null);
        Assert.Equal(followerSees, seenByAlice.WatchedAt != null);

        var seenByBob = await ReviewedBy(bob.Client);
        Assert.Equal(4m, seenByBob.Rating);
        Assert.NotNull(seenByBob.WatchedAt);
    }

    [Fact]
    public async Task UpdateSettings_UnknownDiaryVisibility_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { DiaryVisibility = (DiaryVisibility)7 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
