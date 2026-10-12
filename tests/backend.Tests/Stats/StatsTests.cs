using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.Settings.DTOs;
using backend.Features.Stats.DTOs;
using backend.Features.Users;
using backend.Tests.Infrastructure;

namespace backend.Tests.Stats;

public class StatsTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public StatsTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private async Task LogAsync(TestUser user, DateOnly watchedOn, decimal? score = null, string title = "A film")
    {
        var tmdbId = _factory.Tmdb.AddMovie(Random.Shared.Next(100_000, 999_999), title);
        var response = await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary",
            new LogDiaryEntryDto { WatchedOn = watchedOn, Score = score });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<UserStatsDto> StatsAsync(HttpClient client, int userId, int? year = null) =>
        (await client.GetFromJsonAsync<UserStatsDto>($"/api/users/{userId}/stats" + (year == null ? "" : $"?year={year}")))!;

    [Fact]
    public async Task LastYear_CountsFilmsPerDayAndStreaks()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        await LogAsync(user, Today.AddDays(-1));
        await LogAsync(user, Today.AddDays(-1));
        await LogAsync(user, Today.AddDays(-2));
        await LogAsync(user, Today.AddDays(-5));
        // Outside the last 365 days.
        await LogAsync(user, Today.AddDays(-400));

        var stats = await StatsAsync(user.Client, user.Id);

        Assert.Equal(Today.AddDays(-364), stats.From);
        Assert.Equal(Today, stats.To);
        Assert.Equal(4, stats.Total);
        Assert.Equal(
            [(Today.AddDays(-5), 1), (Today.AddDays(-2), 1), (Today.AddDays(-1), 2)],
            stats.Days.Select(d => (d.Date, d.Count)));
        Assert.Equal(2, stats.LongestStreak);
        // Nothing logged today yet, so the run up to yesterday still counts.
        Assert.Equal(2, stats.CurrentStreak);
        // 13 months, or 12 when the 365 days start on the first of a month.
        Assert.InRange(stats.Months.Count, 12, 13);
        Assert.Equal(4, stats.Months.Sum(m => m.Count));
    }

    [Fact]
    public async Task PastYear_HasMonthsWithAverageRatingsAndNoCurrentStreak()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        await LogAsync(user, new DateOnly(2025, 3, 1), 4);   // Saturday
        await LogAsync(user, new DateOnly(2025, 3, 2), 3);   // Sunday
        await LogAsync(user, new DateOnly(2025, 3, 2));      // Sunday, not rated
        await LogAsync(user, new DateOnly(2025, 7, 10), 5);  // Thursday
        await LogAsync(user, Today);

        var stats = await StatsAsync(user.Client, user.Id, 2025);

        Assert.Equal(new DateOnly(2025, 1, 1), stats.From);
        Assert.Equal(new DateOnly(2025, 12, 31), stats.To);
        Assert.Equal(4, stats.Total);
        Assert.Equal(2, stats.LongestStreak);
        Assert.Null(stats.CurrentStreak);
        Assert.Equal((int)DayOfWeek.Sunday, stats.BusiestWeekday);
        Assert.Equal(12, stats.Months.Count);

        var march = stats.Months.Single(m => m.Month == 3);
        Assert.Equal(3, march.Count);
        Assert.Equal(3.5m, march.AverageRating);
        Assert.Null(stats.Months.Single(m => m.Month == 1).AverageRating);

        Assert.Equal([Today.Year, 2025], stats.Years);
    }

    [Fact]
    public async Task EmptyDiary_HasNoBusiestWeekday()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var stats = await StatsAsync(user.Client, user.Id);

        Assert.Equal(0, stats.Total);
        Assert.Empty(stats.Days);
        Assert.Null(stats.BusiestWeekday);
        Assert.Equal(0, stats.CurrentStreak);
        Assert.Empty(stats.Years);
    }

    [Fact]
    public async Task Year_OutOfRange_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.GetAsync($"/api/users/{user.Id}/stats?year={Today.Year + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Day_ListsThatDaysFilms()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var day = Today.AddDays(-3);
        await LogAsync(user, day, 4, "First of the day");
        await LogAsync(user, day, null, "Second of the day");
        await LogAsync(user, day.AddDays(1), null, "The next day");

        var entries = await user.Client.GetFromJsonAsync<List<DiaryEntryDto>>($"/api/users/{user.Id}/stats/days/{day:yyyy-MM-dd}");

        Assert.Equal(["First of the day", "Second of the day"], entries!.Select(e => e.Title).Order());
    }

    [Fact]
    public async Task HiddenDiary_HidesStatsFromOthers()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        await LogAsync(owner, Today.AddDays(-1));
        (await owner.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { DiaryVisibility = DiaryVisibility.Private }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"/api/users/{owner.Id}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/users/{owner.Id}/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.Client.GetAsync($"/api/users/{owner.Id}/stats/days/{Today.AddDays(-1):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(1, (await StatsAsync(owner.Client, owner.Id)).Total);
    }

    [Fact]
    public async Task PublicDiary_ShowsStatsToVisitors()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        await LogAsync(owner, Today);

        var stats = await StatsAsync(_factory.CreateClient(), owner.Id);

        Assert.Equal(1, stats.Total);
        Assert.Equal(1, stats.CurrentStreak);
    }
}
