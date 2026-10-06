using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.Ratings.DTOs;
using backend.Features.Reviews;
using backend.Features.Reviews.DTOs;
using backend.Features.WatchedMovies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Diary;

public class DiaryControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public DiaryControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static string DiaryUrl(int tmdbId) => $"/api/movies/{tmdbId}/diary";

    private static async Task<List<DiaryEntryDto>> GetDiaryAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<DiaryEntryDto>>("/api/watched"))!;

    [Fact]
    public async Task LogEntry_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(DiaryUrl(1), new LogDiaryEntryDto());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogEntry_NewMovie_SavesRatingStatusAndOneEntry()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6001, "First log");

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 4, Liked = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(await GetDiaryAsync(user.Client));
        Assert.Equal(tmdbId, entry.TmdbId);
        Assert.Equal(4m, entry.Rating);
        Assert.True(entry.Liked);
        var rating = await user.Client.GetFromJsonAsync<RatingDto>($"/api/movies/{tmdbId}/rating");
        Assert.Equal(4m, rating!.Score);
        var status = await user.Client.GetFromJsonAsync<StatusDto>($"/api/movies/{tmdbId}/status");
        Assert.True(status!.Liked);
    }

    [Fact]
    public async Task LogEntry_Again_AddsNewEntryAndKeepsOldSnapshot()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6002, "Logged twice");
        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 2 });

        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 5, Rewatched = true });

        var diary = await GetDiaryAsync(user.Client);
        Assert.Equal([5m, 2m], diary.Select(d => d.Rating));
        Assert.Equal([true, false], diary.Select(d => d.Rewatched));
        Assert.NotEqual(diary[0].Id, diary[1].Id);
    }

    [Fact]
    public async Task LogEntry_WithoutScore_KeepsExistingRating()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6003, "Liked later");
        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 3.5m });

        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Liked = true });

        var latest = (await GetDiaryAsync(user.Client))[0];
        Assert.Equal(3.5m, latest.Rating);
        Assert.True(latest.Liked);
    }

    [Fact]
    public async Task LogEntry_InvalidScore_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6004, "Bad score");

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 3.3m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetDiaryAsync(user.Client));
    }

    [Fact]
    public async Task LogEntry_UnknownTmdbMovie_Returns404()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(6999), new LogDiaryEntryDto());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Watch date ----

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<DiaryEntryDto> LogAsync(TestUser user, int tmdbId, LogDiaryEntryDto dto)
    {
        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiaryEntryDto>())!;
    }

    [Fact]
    public async Task LogEntry_WithWatchedOn_StoresThatDayAndKeepsLatestStatusDate()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6014, "Back-dated");
        await LogAsync(user, tmdbId, new LogDiaryEntryDto());

        var entry = await LogAsync(user, tmdbId, new LogDiaryEntryDto { WatchedOn = new DateOnly(2020, 5, 17) });

        Assert.Equal(new DateTime(2020, 5, 17, 12, 0, 0), entry.WatchedAt);
        var diary = await GetDiaryAsync(user.Client);
        Assert.Equal(entry.Id, diary[^1].Id);
        var status = await user.Client.GetFromJsonAsync<StatusDto>($"/api/movies/{tmdbId}/status");
        Assert.Equal(Today, DateOnly.FromDateTime(status!.WatchedAt));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-200 * 365)]
    public async Task LogEntry_WatchedOnOutOfRange_Returns400(int daysFromToday)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6015, "Bad date");

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { WatchedOn = Today.AddDays(daysFromToday) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Edit ----

    [Fact]
    public async Task UpdateEntry_ChangesOnlyThatEntry()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6016, "Edited");
        var first = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Score = 2, WatchedOn = new DateOnly(2021, 1, 1) });
        await LogAsync(user, tmdbId, new LogDiaryEntryDto { Score = 4 });

        var response = await user.Client.PutAsJsonAsync($"/api/diary/{first.Id}", new UpdateDiaryEntryDto
        {
            WatchedOn = new DateOnly(2021, 2, 3),
            Rating = 3.5m,
            Liked = true,
            Rewatched = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await GetDiaryAsync(user.Client)).Single(d => d.Id == first.Id);
        Assert.Equal(new DateTime(2021, 2, 3, 12, 0, 0), edited.WatchedAt);
        Assert.Equal(3.5m, edited.Rating);
        Assert.True(edited.Liked);
        Assert.True(edited.Rewatched);
        var rating = await user.Client.GetFromJsonAsync<RatingDto>($"/api/movies/{tmdbId}/rating");
        Assert.Equal(4m, rating!.Score);
    }

    [Fact]
    public async Task UpdateEntry_SameDay_KeepsTimeAndCanClearRating()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6017, "Same day");
        var logged = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Score = 5 });

        await user.Client.PutAsJsonAsync($"/api/diary/{logged.Id}", new UpdateDiaryEntryDto { WatchedOn = DateOnly.FromDateTime(logged.WatchedAt) });

        var entry = Assert.Single(await GetDiaryAsync(user.Client));
        Assert.Equal(logged.WatchedAt, entry.WatchedAt);
        Assert.Null(entry.Rating);
    }

    [Fact]
    public async Task UpdateEntry_OtherUsersEntry_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6018, "Not yours");
        var logged = await LogAsync(owner, tmdbId, new LogDiaryEntryDto { Score = 1 });

        var response = await other.Client.PutAsJsonAsync($"/api/diary/{logged.Id}", new UpdateDiaryEntryDto { WatchedOn = Today, Rating = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1m, Assert.Single(await GetDiaryAsync(owner.Client)).Rating);
    }

    [Fact]
    public async Task UpdateEntry_InvalidRatingOrMissingDate_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6019, "Invalid edit");
        var logged = await LogAsync(user, tmdbId, new LogDiaryEntryDto());

        var badRating = await user.Client.PutAsJsonAsync($"/api/diary/{logged.Id}", new UpdateDiaryEntryDto { WatchedOn = Today, Rating = 2.3m });
        var noDate = await user.Client.PutAsJsonAsync($"/api/diary/{logged.Id}", new { Rating = 2 });

        Assert.Equal(HttpStatusCode.BadRequest, badRating.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDate.StatusCode);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteEntry_KeepsStatusWhileOtherEntriesRemain()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6020, "Two viewings");
        var older = await LogAsync(user, tmdbId, new LogDiaryEntryDto { WatchedOn = new DateOnly(2019, 6, 1) });
        var newer = await LogAsync(user, tmdbId, new LogDiaryEntryDto { WatchedOn = new DateOnly(2022, 6, 1) });

        var response = await user.Client.DeleteAsync($"/api/diary/{newer.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([older.Id], (await GetDiaryAsync(user.Client)).Select(d => d.Id));
        var status = await user.Client.GetFromJsonAsync<StatusDto>($"/api/movies/{tmdbId}/status");
        Assert.Equal(older.WatchedAt, status!.WatchedAt);
    }

    [Fact]
    public async Task DeleteEntry_LastEntry_MarksFilmUnwatchedButKeepsRating()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6021, "Only viewing");
        var logged = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Score = 4.5m });

        await user.Client.DeleteAsync($"/api/diary/{logged.Id}");

        Assert.Empty(await GetDiaryAsync(user.Client));
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/movies/{tmdbId}/status")).StatusCode);
        var rating = await user.Client.GetFromJsonAsync<RatingDto>($"/api/movies/{tmdbId}/rating");
        Assert.Equal(4.5m, rating!.Score);
    }

    private async Task<List<ReviewDto>> GetReviewsAsync(int tmdbId) =>
        (await _factory.CreateClient().GetFromJsonAsync<List<ReviewDto>>($"/api/movies/{tmdbId}/reviews"))!;

    [Fact]
    public async Task DeleteEntry_WithReview_DeletesThatReview()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6023, "Reviewed then deleted");
        var older = await LogAsync(user, tmdbId, new LogDiaryEntryDto { WatchedOn = new DateOnly(2020, 1, 1) });
        var reviewed = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Review = "Gone soon" });

        await user.Client.DeleteAsync($"/api/diary/{reviewed.Id}");

        Assert.Empty(await GetReviewsAsync(tmdbId));
        Assert.Equal([older.Id], (await GetDiaryAsync(user.Client)).Select(d => d.Id));
    }

    [Fact]
    public async Task DeleteEntry_WithoutReview_KeepsReviewOfAnotherEntry()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6024, "Review stays");
        var reviewed = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Review = "Keep me" });
        var plain = await LogAsync(user, tmdbId, new LogDiaryEntryDto { Rewatched = true });

        var diary = await GetDiaryAsync(user.Client);
        Assert.True(diary.Single(d => d.Id == reviewed.Id).HasReview);
        Assert.False(diary.Single(d => d.Id == plain.Id).HasReview);

        await user.Client.DeleteAsync($"/api/diary/{plain.Id}");

        Assert.Equal("Keep me", Assert.Single(await GetReviewsAsync(tmdbId)).Text);
    }

    [Fact]
    public async Task DeleteStatus_DeletesReviewsOfItsEntries()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6025, "Unwatched with review");
        await LogAsync(user, tmdbId, new LogDiaryEntryDto { Review = "Never mind" });

        (await user.Client.DeleteAsync($"/api/movies/{tmdbId}/status")).EnsureSuccessStatusCode();

        Assert.Empty(await GetReviewsAsync(tmdbId));
    }

    [Fact]
    public async Task DeleteUser_WithReviewedDiaryEntries_Succeeds()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6026, "Account gone");
        await LogAsync(user, tmdbId, new LogDiaryEntryDto { Score = 3, Review = "Last words" });

        var response = await user.Client.DeleteAsync($"/api/users/{user.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetReviewsAsync(tmdbId));
    }

    [Fact]
    public async Task DeleteEntry_OtherUsersEntry_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6022, "Still mine");
        var logged = await LogAsync(owner, tmdbId, new LogDiaryEntryDto());

        var response = await other.Client.DeleteAsync($"/api/diary/{logged.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await GetDiaryAsync(owner.Client));
    }

    [Fact]
    public async Task UpdateStatus_LogsNewEntryWithCurrentRating()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6005, "Status change");
        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 3 });

        (await user.Client.PutAsJsonAsync($"/api/movies/{tmdbId}/status", new UpdateStatusDto { Liked = true })).EnsureSuccessStatusCode();

        var diary = await GetDiaryAsync(user.Client);
        Assert.Equal(2, diary.Count);
        Assert.True(diary[0].Liked);
        Assert.Equal(3m, diary[0].Rating);
    }

    [Fact]
    public async Task DeleteStatus_RemovesAllEntriesForThatMovie()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var removedId = _factory.Tmdb.AddMovie(6006, "Unwatched");
        var keptId = _factory.Tmdb.AddMovie(6007, "Kept");
        await user.Client.PostAsJsonAsync(DiaryUrl(removedId), new LogDiaryEntryDto { Score = 1 });
        await user.Client.PostAsJsonAsync(DiaryUrl(removedId), new LogDiaryEntryDto { Score = 2 });
        await user.Client.PostAsJsonAsync(DiaryUrl(keptId), new LogDiaryEntryDto());

        (await user.Client.DeleteAsync($"/api/movies/{removedId}/status")).EnsureSuccessStatusCode();

        Assert.Equal([keptId], (await GetDiaryAsync(user.Client)).Select(d => d.TmdbId));
    }

    [Fact]
    public async Task LogEntry_RemovesMovieFromWatchlist()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6008, "Was on watchlist");
        (await user.Client.PostAsync($"/api/watchlist/{tmdbId}", null)).EnsureSuccessStatusCode();

        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto());

        Assert.Empty((await user.Client.GetFromJsonAsync<List<object>>("/api/watchlist"))!);
    }

    [Fact]
    public async Task LogEntry_WithReview_CreatesReviewAndEntry()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6009, "Reviewed");

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 4, Review = "  **Loved** it  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<DiaryEntryDto>())!.HasReview);
        var entry = Assert.Single(await GetDiaryAsync(user.Client));
        Assert.True(entry.HasReview);
        var review = Assert.Single((await user.Client.GetFromJsonAsync<List<ReviewDto>>($"/api/movies/{tmdbId}/reviews"))!);
        Assert.Equal("**Loved** it", review.Text);
        Assert.Equal(4m, review.Score);
        Assert.Null(review.UpdatedAt);
    }

    [Fact]
    public async Task LogEntry_WithReviewAgain_ReplacesReviewAndAddsEntry()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6010, "Reviewed twice");
        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Review = "First take" });

        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Review = "Second take", Rewatched = true });

        Assert.Equal(2, (await GetDiaryAsync(user.Client)).Count);
        var review = Assert.Single((await user.Client.GetFromJsonAsync<List<ReviewDto>>($"/api/movies/{tmdbId}/reviews"))!);
        Assert.Equal("Second take", review.Text);
        Assert.NotNull(review.UpdatedAt);
    }

    [Fact]
    public async Task LogEntry_WithoutReview_KeepsExistingReview()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6011, "Review kept");
        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Review = "Keep me" });

        await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 3, Review = "   " });

        var review = Assert.Single((await user.Client.GetFromJsonAsync<List<ReviewDto>>($"/api/movies/{tmdbId}/reviews"))!);
        Assert.Equal("Keep me", review.Text);
    }

    [Fact]
    public async Task GetUserDiary_IsPublicAndListsThatUsersEntries()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6013, "Public diary");
        await alice.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 3, Review = "Fine" });
        await bob.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Score = 1 });

        var diary = await _factory.CreateClient().GetFromJsonAsync<List<DiaryEntryDto>>($"/api/users/{alice.Id}/diary");

        var entry = Assert.Single(diary!);
        Assert.Equal(3m, entry.Rating);
        Assert.True(entry.HasReview);
    }

    [Fact]
    public async Task GetUserDiary_UnknownUser_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/999999/diary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LogEntry_ReviewTooLong_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(6012, "Too long");

        var response = await user.Client.PostAsJsonAsync(DiaryUrl(tmdbId), new LogDiaryEntryDto { Review = new string('a', Review.MaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetDiaryAsync(user.Client));
    }
}
