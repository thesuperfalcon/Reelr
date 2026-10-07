using System.Net;
using System.Net.Http.Json;
using backend.Features.Activity;
using backend.Features.Activity.DTOs;
using backend.Features.Diary.DTOs;
using backend.Features.MovieLists.DTOs;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Activity;

public class ActivityControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public ActivityControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    // ---- Helpers ----

    private static async Task<DiaryEntryDto> LogAsync(TestUser user, int tmdbId, LogDiaryEntryDto? dto = null)
    {
        var response = await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", dto ?? new LogDiaryEntryDto());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiaryEntryDto>())!;
    }

    private static async Task FollowAsync(TestUser follower, TestUser followed) =>
        (await follower.Client.PostAsync($"/api/users/{followed.Id}/follow", null)).EnsureSuccessStatusCode();

    private static async Task<MovieListDto> CreateListAsync(TestUser user, string name, bool isPublic = true)
    {
        var response = await user.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = name, IsPublic = isPublic });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MovieListDto>())!;
    }

    private static async Task AddToListAsync(TestUser user, int listId, int tmdbId) =>
        (await user.Client.PostAsJsonAsync($"/api/lists/{listId}/movies", new AddMovieToListDto { TmdbId = tmdbId })).EnsureSuccessStatusCode();

    private static async Task AddToWatchlistAsync(TestUser user, int tmdbId) =>
        (await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null)).EnsureSuccessStatusCode();

    private static async Task<ActivityPageDto> GetPageAsync(HttpClient client, string feed, string? cursor = null, int? limit = null)
    {
        var query = new List<string>();
        if (cursor != null) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        if (limit != null) query.Add($"limit={limit}");

        var response = await client.GetAsync($"/api/feed/{feed}{(query.Count > 0 ? "?" + string.Join('&', query) : "")}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityPageDto>())!;
    }

    // The community feed is shared by every test in the class, so assertions look only at the test's own users.
    private static List<ActivityItemDto> By(ActivityPageDto page, params TestUser[] actors) =>
        page.Items.Where(i => actors.Any(a => a.Id == i.Actor.Id)).ToList();

    private Task<int> SetLoggedAtAsync(int entryId, DateTime loggedAt) =>
        _factory.WithContextAsync(async context =>
        {
            var entry = await context.DiaryEntries.SingleAsync(d => d.Id == entryId);
            entry.LoggedAt = loggedAt;
            return await context.SaveChangesAsync();
        });

    // ---- Following ----

    [Fact]
    public async Task Following_ShowsEveryKindFromFollowedPeopleOnly()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var carol = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var (watched, reviewed, listed, wanted, other) = (
            _factory.Tmdb.AddMovie(7101, "Watched"), _factory.Tmdb.AddMovie(7102, "Reviewed"),
            _factory.Tmdb.AddMovie(7103, "Listed"), _factory.Tmdb.AddMovie(7104, "Wanted"), _factory.Tmdb.AddMovie(7105, "Other"));

        await LogAsync(bob, watched, new LogDiaryEntryDto { Score = 3 });
        await LogAsync(bob, reviewed, new LogDiaryEntryDto { Score = 5, Review = "Loved it" });
        var list = await CreateListAsync(bob, "Favourites");
        await AddToListAsync(bob, list.Id, listed);
        await AddToWatchlistAsync(bob, wanted);
        await LogAsync(carol, other);

        var page = await GetPageAsync(alice.Client, "following");

        Assert.All(page.Items, i => Assert.Equal(bob.Id, i.Actor.Id));
        Assert.Equal(
            new[] { ActivityTypes.Watched, ActivityTypes.Reviewed, ActivityTypes.ListCreated, ActivityTypes.ListAdded, ActivityTypes.WatchlistAdded }.Order(),
            page.Items.Select(i => i.Type).Order());
        var review = Assert.Single(page.Items, i => i.Type == ActivityTypes.Reviewed);
        Assert.Equal("Loved it", review.Review!.Excerpt);
        Assert.Equal(5m, review.Rating);
        Assert.Equal(reviewed, review.Movie!.TmdbId);
        Assert.Equal("Favourites", Assert.Single(page.Items, i => i.Type == ActivityTypes.ListAdded).List!.Name);
        Assert.Equal(wanted, Assert.Single(page.Items, i => i.Type == ActivityTypes.WatchlistAdded).Movie!.TmdbId);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Following_HidesPrivateListsAndPrivateWatchlists()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var list = await CreateListAsync(bob, "Secret", isPublic: false);
        await AddToListAsync(bob, list.Id, _factory.Tmdb.AddMovie(7111, "Hidden"));
        await AddToWatchlistAsync(bob, _factory.Tmdb.AddMovie(7112, "Private wish"));
        await _factory.WithContextAsync(async context =>
        {
            (await context.Users.SingleAsync(u => u.Id == bob.Id)).WatchlistVisibility = WatchlistVisibility.Private;
            return await context.SaveChangesAsync();
        });

        var page = await GetPageAsync(alice.Client, "following");

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Following_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/feed/following");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Community ----

    [Fact]
    public async Task Community_ShowsOnlyPublicKindsAndLeavesOutYourOwn()
    {
        var bob = await _factory.CreateAuthenticatedAsync();
        await LogAsync(bob, _factory.Tmdb.AddMovie(7121, "Seen"));
        await LogAsync(bob, _factory.Tmdb.AddMovie(7122, "Written about"), new LogDiaryEntryDto { Review = "Fine" });
        var list = await CreateListAsync(bob, "Public picks");
        await AddToListAsync(bob, list.Id, _factory.Tmdb.AddMovie(7123, "Picked"));
        await AddToWatchlistAsync(bob, _factory.Tmdb.AddMovie(7124, "Someday"));
        await CreateListAsync(bob, "Empty list");

        var anonymous = By(await GetPageAsync(_factory.CreateClient(), "community"), bob);
        var own = By(await GetPageAsync(bob.Client, "community"), bob);

        Assert.Equal(
            [ActivityTypes.ListCreated, ActivityTypes.Reviewed, ActivityTypes.Watched],
            anonymous.Select(i => i.Type).Order());
        Assert.Equal("Public picks", Assert.Single(anonymous, i => i.Type == ActivityTypes.ListCreated).List!.Name);
        Assert.Empty(own);
    }

    [Fact]
    public async Task BackdatedLogs_StayOutOfCommunityButShowInFollowing()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await LogAsync(bob, _factory.Tmdb.AddMovie(7131, "Seen years ago"),
            new LogDiaryEntryDto { WatchedOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-100) });

        Assert.Empty(By(await GetPageAsync(alice.Client, "community"), bob));
        Assert.Single(By(await GetPageAsync(alice.Client, "following"), bob));
    }

    [Fact]
    public async Task OldActivity_LeavesCommunityAfterItsWindow()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var entry = await LogAsync(bob, _factory.Tmdb.AddMovie(7141, "Last season"));
        await SetLoggedAtAsync(entry.Id, DateTime.UtcNow.AddDays(-40));

        Assert.Empty(By(await GetPageAsync(alice.Client, "community"), bob));
        Assert.Single(By(await GetPageAsync(alice.Client, "following"), bob));
    }

    // ---- Merging and grouping ----

    [Fact]
    public async Task ReviewedLog_IsOneItem_AndTurnsIntoWatchedWhenTheReviewIsDeleted()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var tmdbId = _factory.Tmdb.AddMovie(7151, "Reviewed once");
        await LogAsync(bob, tmdbId, new LogDiaryEntryDto { Score = 4, Review = new string('a', 200) + " " + new string('b', 400) });

        var item = Assert.Single((await GetPageAsync(alice.Client, "following")).Items);
        Assert.Equal(ActivityTypes.Reviewed, item.Type);
        Assert.True(item.Review!.IsTruncated);
        Assert.Equal(new string('a', 200), item.Review.Excerpt);

        (await bob.Client.DeleteAsync($"/api/reviews/{item.Review.Id}")).EnsureSuccessStatusCode();

        var after = Assert.Single((await GetPageAsync(alice.Client, "following")).Items);
        Assert.Equal(ActivityTypes.Watched, after.Type);
        Assert.Equal(4m, after.Rating);
    }

    // ---- Own activity in Following ----

    [Fact]
    public async Task Following_IncludesOwnActivity_PrivateOnesToo_ByDefault()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7301, "My own log");
        await LogAsync(alice, tmdbId);
        await CreateListAsync(alice, "Secret list", isPublic: false);

        var page = await GetPageAsync(alice.Client, "following");

        Assert.Equal([ActivityTypes.ListCreated, ActivityTypes.Watched], page.Items.Select(i => i.Type));
        Assert.All(page.Items, i => Assert.Equal(alice.Id, i.Actor.Id));
    }

    [Fact]
    public async Task Following_LeavesOutOwnActivity_WhenTheSettingIsOff()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await LogAsync(alice, _factory.Tmdb.AddMovie(7302, "Mine"));
        await LogAsync(bob, _factory.Tmdb.AddMovie(7303, "Bob's"));

        (await alice.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { ShowOwnActivity = false })).EnsureSuccessStatusCode();

        var item = Assert.Single((await GetPageAsync(alice.Client, "following")).Items);
        Assert.Equal(bob.Id, item.Actor.Id);
    }

    [Fact]
    public async Task Following_IncludeOwnQuery_OverridesTheSetting()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        await LogAsync(alice, _factory.Tmdb.AddMovie(7304, "Hidden by query"), new LogDiaryEntryDto { Review = "Mine" });

        var response = await alice.Client.GetAsync("/api/feed/following?types=reviewed&includeOwn=false");

        response.EnsureSuccessStatusCode();
        Assert.Empty((await response.Content.ReadFromJsonAsync<ActivityPageDto>())!.Items);
    }

    [Fact]
    public async Task ReviewedItem_WithSpoilers_HasNoExcerpt_AndCarriesLikeAndCommentCounts()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var tmdbId = _factory.Tmdb.AddMovie(7152, "Spoiled");
        await LogAsync(bob, tmdbId, new LogDiaryEntryDto { Review = "The butler did it", ContainsSpoilers = true });
        var reviewId = Assert.Single((await GetPageAsync(alice.Client, "following")).Items).Review!.Id;
        (await alice.Client.PutAsync($"/api/reviews/{reviewId}/like", null)).EnsureSuccessStatusCode();
        (await alice.Client.PostAsJsonAsync($"/api/reviews/{reviewId}/comments", new { Text = "No!" })).EnsureSuccessStatusCode();

        var review = Assert.Single((await GetPageAsync(alice.Client, "following")).Items).Review!;

        Assert.True(review.ContainsSpoilers);
        Assert.Equal(string.Empty, review.Excerpt);
        Assert.Equal(1, review.LikeCount);
        Assert.Equal(1, review.CommentCount);
    }

    [Fact]
    public async Task LogsCloseTogether_AreGrouped_AndSplitWhenFarApart()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var first = await LogAsync(bob, _factory.Tmdb.AddMovie(7161, "One"));
        await LogAsync(bob, _factory.Tmdb.AddMovie(7162, "Two"));
        await LogAsync(bob, _factory.Tmdb.AddMovie(7163, "Three"));

        var grouped = Assert.Single((await GetPageAsync(alice.Client, "following")).Items);
        Assert.Equal(3, grouped.GroupCount);
        Assert.Equal(["Three", "Two", "One"], grouped.GroupMovies.Select(m => m.Title));

        await SetLoggedAtAsync(first.Id, DateTime.UtcNow.AddHours(-3));

        var split = (await GetPageAsync(alice.Client, "following")).Items;
        Assert.Equal([2, 1], split.Select(i => i.GroupCount));
    }

    // ---- Paging ----

    [Fact]
    public async Task Paging_ReturnsEveryItemOnceInOrder_EvenWithEqualTimes()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var carol = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await FollowAsync(alice, carol);
        var start = DateTime.UtcNow.AddDays(-1);
        var entryIds = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            // Hours apart, so nothing groups; bob and carol share each timestamp to force ties.
            var at = start.AddHours(i * 2);
            foreach (var user in new[] { bob, carol })
            {
                var entry = await LogAsync(user, _factory.Tmdb.AddMovie(7170 + entryIds.Count, $"Film {entryIds.Count}"));
                await SetLoggedAtAsync(entry.Id, at);
                entryIds.Add(entry.Id);
            }
        }

        var seen = new List<ActivityItemDto>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var page = await GetPageAsync(alice.Client, "following", cursor, limit: 3);
            Assert.InRange(page.Items.Count, 1, 3);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor != null && pages < 10);

        Assert.Null(cursor);
        Assert.Equal(entryIds.Select(id => $"diary:{id}").Order(), seen.Select(i => i.Id).Order());
        Assert.Equal(seen.Count, seen.Select(i => i.Id).Distinct().Count());
        Assert.Equal(seen.Select(i => i.OccurredAt).OrderDescending(), seen.Select(i => i.OccurredAt));
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("MTIz")]
    public async Task InvalidCursor_Returns400(string cursor)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/feed/community?cursor={cursor}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Limit_IsClampedToAtLeastOne()
    {
        var page = await GetPageAsync(_factory.CreateClient(), "community", limit: 0);

        Assert.True(page.Items.Count <= 1);
    }

    // ---- Filtering by kind ----

    [Fact]
    public async Task Types_LimitsBothFeedsToTheKindsAsked_AndIgnoresUnknownKinds()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await LogAsync(bob, _factory.Tmdb.AddMovie(7201, "Just seen"));
        await LogAsync(bob, _factory.Tmdb.AddMovie(7202, "Written up"), new LogDiaryEntryDto { Review = "Worth it" });
        await AddToWatchlistAsync(bob, _factory.Tmdb.AddMovie(7203, "Later"));

        async Task<List<string>> TypesIn(HttpClient client, string feed, string types)
        {
            var response = await client.GetAsync($"/api/feed/{feed}?types={types}");
            response.EnsureSuccessStatusCode();
            var page = (await response.Content.ReadFromJsonAsync<ActivityPageDto>())!;
            return By(page, bob).Select(i => i.Type).Order().ToList();
        }

        Assert.Equal([ActivityTypes.Reviewed], await TypesIn(alice.Client, "following", "reviewed"));
        Assert.Equal([ActivityTypes.Reviewed, ActivityTypes.WatchlistAdded], await TypesIn(alice.Client, "following", "watchlistAdded,reviewed"));
        Assert.Equal([ActivityTypes.Reviewed], await TypesIn(_factory.CreateClient(), "community", "reviewed,nonsense"));
        Assert.Empty(await TypesIn(alice.Client, "following", "nonsense"));
    }

    // ---- New from friends ----

    private async Task<List<FollowingFilmDto>> GetFilmsAsync(HttpClient client, int? limit = null)
    {
        var response = await client.GetAsync($"/api/feed/following/films{(limit != null ? $"?limit={limit}" : "")}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<FollowingFilmDto>>())!;
    }

    [Fact]
    public async Task FollowingFilms_ShowsEachFilmOnce_NewestFirst_WithTheNewestWatcherFirst()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var carol = await _factory.CreateAuthenticatedAsync();
        var dave = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        await FollowAsync(alice, carol);
        var (shared, older, unseen) = (
            _factory.Tmdb.AddMovie(7211, "Shared"), _factory.Tmdb.AddMovie(7212, "Older"), _factory.Tmdb.AddMovie(7213, "Not followed"));

        var first = await LogAsync(bob, older, new LogDiaryEntryDto { Score = 2 });
        await SetLoggedAtAsync(first.Id, DateTime.UtcNow.AddHours(-5));
        var bobShared = await LogAsync(bob, shared, new LogDiaryEntryDto { Score = 3 });
        await SetLoggedAtAsync(bobShared.Id, DateTime.UtcNow.AddHours(-2));
        var bobRewatch = await LogAsync(bob, shared, new LogDiaryEntryDto { Score = 4 });
        await SetLoggedAtAsync(bobRewatch.Id, DateTime.UtcNow.AddHours(-1));
        await LogAsync(carol, shared, new LogDiaryEntryDto { Score = 5, Liked = true });
        await LogAsync(dave, unseen);

        var films = await GetFilmsAsync(alice.Client);

        Assert.Equal([shared, older], films.Select(f => f.Movie.TmdbId));
        var top = films[0];
        Assert.Equal(2, top.WatcherCount);
        Assert.Equal([carol.Id, bob.Id], top.Watchers.Select(w => w.Actor.Id));
        Assert.Equal(5m, top.Watchers[0].Rating);
        Assert.True(top.Watchers[0].Liked);
        // Bob's newest log of the film counts, not the first one.
        Assert.Equal(4m, top.Watchers[1].Rating);
        Assert.Single(await GetFilmsAsync(alice.Client, limit: 1));
    }

    [Fact]
    public async Task FollowingFilms_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/feed/following/films");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Deleted and hidden content ----

    [Fact]
    public async Task DeletedOrHiddenContent_AndUnfollowing_LeaveTheFeed()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await FollowAsync(alice, bob);
        var logged = _factory.Tmdb.AddMovie(7191, "Logged");
        var listed = _factory.Tmdb.AddMovie(7192, "Listed");
        var wanted = _factory.Tmdb.AddMovie(7193, "Wanted");
        var entry = await LogAsync(bob, logged);
        var list = await CreateListAsync(bob, "Short-lived");
        await AddToListAsync(bob, list.Id, listed);
        await AddToWatchlistAsync(bob, wanted);
        Assert.Equal(4, (await GetPageAsync(alice.Client, "following")).Items.Count);

        (await bob.Client.DeleteAsync($"/api/diary/{entry.Id}")).EnsureSuccessStatusCode();
        (await bob.Client.PutAsJsonAsync($"/api/lists/{list.Id}", new UpdateMovieListDto { IsPublic = false })).EnsureSuccessStatusCode();
        Assert.Equal([ActivityTypes.WatchlistAdded], (await GetPageAsync(alice.Client, "following")).Items.Select(i => i.Type));

        (await bob.Client.DeleteAsync($"/api/watchlist/{wanted}")).EnsureSuccessStatusCode();
        await AddToWatchlistAsync(bob, logged);
        Assert.Equal(logged, Assert.Single((await GetPageAsync(alice.Client, "following")).Items).Movie!.TmdbId);

        (await alice.Client.DeleteAsync($"/api/users/{bob.Id}/follow")).EnsureSuccessStatusCode();
        Assert.Empty((await GetPageAsync(alice.Client, "following")).Items);
    }
}
