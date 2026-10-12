using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.Movies;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Movies;

public class MovieRefreshTests : IClassFixture<ReelrApiFactory>
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly ReelrApiFactory _factory;

    public MovieRefreshTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private async Task StoreAsync(int tmdbId)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        (await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto())).EnsureSuccessStatusCode();
    }

    private Task<int> AgeAsync(int tmdbId, TimeSpan age) => _factory.WithContextAsync(context =>
        context.Movies.Where(m => m.TmdbId == tmdbId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.RefreshedAt, DateTime.UtcNow - age)));

    private Task<Movie> GetAsync(int tmdbId) =>
        _factory.WithContextAsync(context => context.Movies.AsNoTracking().SingleAsync(m => m.TmdbId == tmdbId));

    private async Task<int> RefreshAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MovieCatalog>()
            .RefreshStaleAsync(MaxAge, batchSize: 1000, CancellationToken.None);
    }

    [Fact]
    public async Task Refresh_CopiesStaleFilmsAgainAndLeavesFreshOnes()
    {
        var stale = _factory.Tmdb.AddMovie(7801, "Old title");
        var fresh = _factory.Tmdb.AddMovie(7802, "Fresh title");
        await StoreAsync(stale);
        await StoreAsync(fresh);
        await AgeAsync(stale, TimeSpan.FromDays(40));
        _factory.Tmdb.AddMovie(stale, "New title");
        _factory.Tmdb.AddMovie(fresh, "Changed but not due");

        await RefreshAsync();

        var refreshed = await GetAsync(stale);
        Assert.Equal("New title", refreshed.Title);
        Assert.True(refreshed.RefreshedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("Fresh title", (await GetAsync(fresh)).Title);
    }

    [Fact]
    public async Task Refresh_FilmTmdbFailsOn_StaysDueWithoutBlockingOthers()
    {
        var failing = _factory.Tmdb.AddMovie(7803, "Waiting title");
        var other = _factory.Tmdb.AddMovie(7804, "Other old title");
        await StoreAsync(failing);
        await StoreAsync(other);
        await AgeAsync(failing, TimeSpan.FromDays(50));
        await AgeAsync(other, TimeSpan.FromDays(40));
        _factory.Tmdb.SetResponse($"movie/{failing}", null, HttpStatusCode.InternalServerError);
        _factory.Tmdb.AddMovie(other, "Other new title");

        await RefreshAsync();

        var movie = await GetAsync(failing);
        Assert.Equal("Waiting title", movie.Title);
        Assert.True(movie.RefreshedAt < DateTime.UtcNow.AddDays(-30));
        Assert.Equal("Other new title", (await GetAsync(other)).Title);
    }
}
