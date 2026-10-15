using System.Net.Http.Json;
using backend.Data;
using backend.Features.Activity;
using backend.Features.Activity.DTOs;
using backend.Features.Diary.DTOs;
using backend.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Activity;

// The community feed's cache on, as in production, but only for a second so a test can wait it out.
public class CommunityCacheApiFactory : ReelrApiFactory
{
    protected override int CommunityCacheSeconds => 1;
}

public class AnonymousCommunityTests : IClassFixture<CommunityCacheApiFactory>
{
    private readonly CommunityCacheApiFactory _factory;
    private readonly DatabaseActivity _database;

    public AnonymousCommunityTests(CommunityCacheApiFactory factory)
    {
        _factory = factory;
        _database = factory.Services.GetRequiredService<DatabaseActivity>();
    }

    private static async Task LogReviewAsync(TestUser user, int tmdbId) =>
        (await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary", new LogDiaryEntryDto { Review = "Worth it" }))
            .EnsureSuccessStatusCode();

    private static async Task<List<ActivityItemDto>> CommunityByAsync(HttpClient client, TestUser actor)
    {
        var page = (await client.GetFromJsonAsync<ActivityPageDto>("/api/feed/community"))!;
        return page.Items.Where(i => i.Actor.Id == actor.Id).ToList();
    }

    [Fact]
    public async Task A_logged_out_visitor_gets_the_page_without_using_the_database_again()
    {
        var bob = await _factory.CreateAuthenticatedAsync();
        await LogReviewAsync(bob, _factory.Tmdb.AddMovie(7501, "Seen by bob"));
        // Past the short cache, which an earlier test may have filled, so this read goes to the database.
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        await CommunityByAsync(_factory.CreateClient(), bob);
        var version = _database.Version;

        var items = await CommunityByAsync(_factory.CreateClient(), bob);

        Assert.Single(items);
        Assert.Equal(version, _database.Version);
    }

    [Fact]
    public async Task A_change_after_the_page_was_read_shows_once_the_short_cache_ends()
    {
        var bob = await _factory.CreateAuthenticatedAsync();
        await LogReviewAsync(bob, _factory.Tmdb.AddMovie(7511, "First"));
        await CommunityByAsync(_factory.CreateClient(), bob);

        await LogReviewAsync(bob, _factory.Tmdb.AddMovie(7512, "Second"));
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        Assert.Equal(2, (await CommunityByAsync(_factory.CreateClient(), bob)).Count);
    }

    [Fact]
    public async Task The_refresh_before_the_database_goes_idle_brings_the_page_up_to_date()
    {
        var bob = await _factory.CreateAuthenticatedAsync();
        await CommunityByAsync(_factory.CreateClient(), bob);
        await LogReviewAsync(bob, _factory.Tmdb.AddMovie(7521, "Logged late"));

        using (var scope = _factory.Services.CreateScope())
        {
            var refresh = scope.ServiceProvider.GetServices<IBeforeDatabaseIdle>().OfType<AnonymousCommunityRefresh>().Single();
            await refresh.RunAsync(CancellationToken.None);
        }

        var version = _database.Version;

        Assert.Single(await CommunityByAsync(_factory.CreateClient(), bob));
        Assert.Equal(version, _database.Version);
    }

    [Fact]
    public async Task A_signed_in_member_does_not_get_the_logged_out_page()
    {
        var bob = await _factory.CreateAuthenticatedAsync();
        await LogReviewAsync(bob, _factory.Tmdb.AddMovie(7531, "Own review"));

        Assert.Single(await CommunityByAsync(_factory.CreateClient(), bob));
        Assert.Empty(await CommunityByAsync(bob.Client, bob));
    }
}
