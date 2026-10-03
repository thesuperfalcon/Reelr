using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.Ratings.DTOs;
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
}
