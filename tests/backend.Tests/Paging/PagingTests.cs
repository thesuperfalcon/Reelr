using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.MovieLists.DTOs;
using backend.Features.Paging;
using backend.Features.Ratings;
using backend.Features.Reviews.DTOs;
using backend.Features.Users;
using backend.Features.Users.DTOs;
using backend.Features.WatchlistItems.DTOs;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Paging;

public class PagingTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public PagingTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    // Follows nextCursor until the last page and returns every page.
    private static async Task<List<PageDto<T>>> GetAllPagesAsync<T>(HttpClient client, string url, int limit)
    {
        var pages = new List<PageDto<T>>();
        string? cursor = null;

        do
        {
            var separator = url.Contains('?') ? '&' : '?';
            var pageUrl = $"{url}{separator}limit={limit}" + (cursor == null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = (await client.GetFromJsonAsync<PageDto<T>>(pageUrl))!;
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor != null && pages.Count < 50);

        return pages;
    }

    private static async Task LogAsync(TestUser user, int tmdbId, DateOnly watchedOn, string? review = null)
    {
        var response = await user.Client.PostAsJsonAsync(
            $"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto { WatchedOn = watchedOn, Review = review });
        response.EnsureSuccessStatusCode();
    }

    // ---- Pages ----

    [Fact]
    public async Task Diary_PagesThroughEveryEntryOnceInOrder()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7701, "Paged diary");
        var day = new DateOnly(2024, 1, 1);

        for (var i = 0; i < 5; i++)
        {
            await LogAsync(user, tmdbId, day.AddDays(i));
        }

        // Two logs on the same day are ordered by id, so the tie is broken the same way on every page.
        await LogAsync(user, tmdbId, day.AddDays(2));

        var pages = await GetAllPagesAsync<DiaryEntryDto>(user.Client, "/api/watched", limit: 2);

        Assert.Equal([2, 2, 2], pages.Select(p => p.Items.Count));
        Assert.Null(pages[^1].NextCursor);
        var entries = pages.SelectMany(p => p.Items).ToList();
        Assert.Equal(entries.Count, entries.Select(e => e.Id).Distinct().Count());
        Assert.Equal(entries.OrderByDescending(e => e.WatchedAt).ThenByDescending(e => e.Id).Select(e => e.Id), entries.Select(e => e.Id));
    }

    [Fact]
    public async Task Diary_EntryAddedWhilePaging_DoesNotRepeatOrSkipOthers()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7702, "Moving diary");

        for (var i = 0; i < 4; i++)
        {
            await LogAsync(user, tmdbId, new DateOnly(2023, 6, 1 + i));
        }

        var first = (await user.Client.GetFromJsonAsync<PageDto<DiaryEntryDto>>("/api/watched?limit=2"))!;

        // A new log on top would push every entry one step down with offset paging.
        await LogAsync(user, tmdbId, new DateOnly(2023, 7, 1));

        var second = (await user.Client.GetFromJsonAsync<PageDto<DiaryEntryDto>>(
            $"/api/watched?limit=2&cursor={Uri.EscapeDataString(first.NextCursor!)}"))!;

        Assert.Equal([new DateTime(2023, 6, 2, 12, 0, 0), new DateTime(2023, 6, 1, 12, 0, 0)], second.Items.Select(e => e.WatchedAt));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Followers_PageInNameOrder()
    {
        var target = await _factory.CreateAuthenticatedAsync();
        var followers = new List<TestUser>();

        for (var i = 0; i < 5; i++)
        {
            var follower = await _factory.CreateAuthenticatedAsync();
            (await follower.Client.PostAsync($"/api/users/{target.Id}/follow", null)).EnsureSuccessStatusCode();
            followers.Add(follower);
        }

        var pages = await GetAllPagesAsync<UserSummaryDto>(_factory.CreateClient(), $"/api/users/{target.Id}/followers", limit: 2);

        Assert.Equal(3, pages.Count);
        Assert.Equal(
            followers.Select(f => f.Username).Order(StringComparer.OrdinalIgnoreCase),
            pages.SelectMany(p => p.Items).Select(u => u.UserName));
    }

    [Fact]
    public async Task Comments_PageOldestFirst()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7703, "Talked about");
        await LogAsync(author, tmdbId, new DateOnly(2024, 2, 1), review: "Worth a talk.");
        var review = (await author.Client.GetFromJsonAsync<ReviewDto>($"/api/movies/{tmdbId}/reviews/mine"))!;

        for (var i = 1; i <= 5; i++)
        {
            (await author.Client.PostAsJsonAsync($"/api/reviews/{review.Id}/comments", new { Text = $"Comment {i}" })).EnsureSuccessStatusCode();
        }

        var pages = await GetAllPagesAsync<ReviewCommentDto>(_factory.CreateClient(), $"/api/reviews/{review.Id}/comments", limit: 2);

        Assert.Equal(["Comment 1", "Comment 2", "Comment 3", "Comment 4", "Comment 5"], pages.SelectMany(p => p.Items).Select(c => c.Text));
    }

    [Fact]
    public async Task ListMovies_PageNewestAdditionFirst()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = (await (await owner.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "Paged list" }))
            .Content.ReadFromJsonAsync<MovieListDto>())!;

        for (var i = 0; i < 5; i++)
        {
            var tmdbId = _factory.Tmdb.AddMovie(7710 + i, $"List film {i}");
            (await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new { TmdbId = tmdbId })).EnsureSuccessStatusCode();
        }

        var pages = await GetAllPagesAsync<MovieListItemDto>(owner.Client, $"/api/lists/{list.Id}/movies", limit: 3);

        Assert.Equal([3, 2], pages.Select(p => p.Items.Count));
        Assert.Equal([7714, 7713, 7712, 7711, 7710], pages.SelectMany(p => p.Items).Select(m => m.TmdbId));
    }

    [Theory]
    [InlineData("cursor=not-a-cursor")]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    public async Task InvalidPaging_Returns400(string query)
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.GetAsync($"/api/watched?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DefaultLimit_CapsThePage()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7704, "Logged a lot");

        for (var i = 0; i < PageQuery.DefaultLimit + 1; i++)
        {
            await LogAsync(user, tmdbId, new DateOnly(2022, 1, 1).AddDays(i));
        }

        var page = (await user.Client.GetFromJsonAsync<PageDto<DiaryEntryDto>>("/api/watched"))!;

        Assert.Equal(PageQuery.DefaultLimit, page.Items.Count);
        Assert.NotNull(page.NextCursor);
    }

    // ---- What the pages replaced ----

    [Fact]
    public async Task WatchlistState_TellsWhetherAFilmIsOnTheWatchlist()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7720, "Saved for later");

        Assert.False((await user.Client.GetFromJsonAsync<WatchlistStateDto>($"/api/watchlist/{tmdbId}"))!.OnWatchlist);
        (await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null)).EnsureSuccessStatusCode();
        Assert.True((await user.Client.GetFromJsonAsync<WatchlistStateDto>($"/api/watchlist/{tmdbId}"))!.OnWatchlist);
    }

    [Fact]
    public async Task WatchedFilms_ListsEachWatchedFilmOnce()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var first = _factory.Tmdb.AddMovie(7721, "Seen twice");
        var second = _factory.Tmdb.AddMovie(7722, "Seen once");
        await LogAsync(user, first, new DateOnly(2024, 3, 1));
        await LogAsync(user, first, new DateOnly(2024, 3, 2));
        await LogAsync(user, second, new DateOnly(2024, 3, 3));

        var films = (await user.Client.GetFromJsonAsync<List<int>>("/api/watched/films"))!;

        Assert.Equal([first, second], films.Order());
    }

    [Fact]
    public async Task MyReview_Returns204WithoutAReviewAndTheReviewWithOne()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7723, "Mine to review");

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.GetAsync($"/api/movies/{tmdbId}/reviews/mine")).StatusCode);

        await LogAsync(user, tmdbId, new DateOnly(2024, 4, 1), review: "My take.");

        var review = (await user.Client.GetFromJsonAsync<ReviewDto>($"/api/movies/{tmdbId}/reviews/mine"))!;
        Assert.Equal("My take.", review.Text);
        Assert.Equal(user.Id, review.UserId);
    }

    [Fact]
    public async Task MovieReviews_CountEveryReviewNotOnlyThePage()
    {
        var tmdbId = _factory.Tmdb.AddMovie(7724, "Much reviewed");

        for (var i = 0; i < 3; i++)
        {
            await LogAsync(await _factory.CreateAuthenticatedAsync(), tmdbId, new DateOnly(2024, 5, 1), review: $"Review {i}");
        }

        var page = (await _factory.CreateClient().GetFromJsonAsync<MovieReviewPageDto>($"/api/movies/{tmdbId}/reviews?limit=1"))!;

        Assert.Single(page.Items);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task List_CountsTheFilmsTheViewerHasWatched()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var viewer = await _factory.CreateAuthenticatedAsync();
        var list = (await (await owner.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "Seen any?" }))
            .Content.ReadFromJsonAsync<MovieListDto>())!;
        var seen = _factory.Tmdb.AddMovie(7725, "Seen by viewer");
        var unseen = _factory.Tmdb.AddMovie(7726, "Not seen");
        await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new { TmdbId = seen });
        await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new { TmdbId = unseen });
        await LogAsync(viewer, seen, new DateOnly(2024, 6, 1));

        var asViewer = (await viewer.Client.GetFromJsonAsync<MovieListDto>($"/api/lists/{list.Id}"))!;
        var anonymous = (await _factory.CreateClient().GetFromJsonAsync<MovieListDto>($"/api/lists/{list.Id}"))!;

        Assert.Equal(1, asViewer.WatchedCount);
        Assert.Null(anonymous.WatchedCount);
    }

    // ---- Database rules ----

    [Theory]
    [InlineData(0)]
    [InlineData(5.5)]
    [InlineData(3.3)]
    public async Task Database_RefusesScoresOutsideHalfStars(decimal score)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7730, "Rated directly");
        await LogAsync(user, tmdbId, new DateOnly(2024, 7, 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => _factory.WithContextAsync(async context =>
        {
            var movieId = await context.Movies.Where(m => m.TmdbId == tmdbId).Select(m => m.Id).SingleAsync();
            context.Ratings.Add(new Rating { UserId = user.Id, MovieId = movieId, Score = score });
            return await context.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Database_RefusesFollowingYourself()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => _factory.WithContextAsync(context =>
        {
            context.Set<Follow>().Add(new Follow { FollowerId = user.Id, FollowedId = user.Id });
            return context.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Database_MarksLogsSavedLongAfterTheWatchDateAsBackdated()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7731, "Back-filled");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await LogAsync(user, tmdbId, today);
        await LogAsync(user, tmdbId, today.AddDays(-30));

        var flags = await _factory.WithContextAsync(context => context.DiaryEntries
            .Where(d => d.UserId == user.Id)
            .OrderByDescending(d => d.WatchedAt)
            .Select(d => d.IsBackdated)
            .ToListAsync());

        Assert.Equal([false, true], flags);
    }

    [Fact]
    public async Task MovieCatalog_CutsTmdbValuesThatDoNotFit()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(7732, new string('T', 600));

        await LogAsync(user, tmdbId, new DateOnly(2024, 8, 1));

        var title = await _factory.WithContextAsync(context =>
            context.Movies.Where(m => m.TmdbId == tmdbId).Select(m => m.Title).SingleAsync());
        Assert.Equal(new string('T', 500), title);
    }
}
