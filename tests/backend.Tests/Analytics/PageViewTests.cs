using System.Net;
using System.Net.Http.Json;
using backend.Features.Analytics.DTOs;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace backend.Tests.Analytics;

/// <summary>
/// The first user created in this factory's database has id 1, which is configured as the admin.
/// </summary>
public class AnalyticsApiFactory : ReelrApiFactory
{
    public AnalyticsApiFactory()
    {
        Admin = new Lazy<Task<TestUser>>(() => this.CreateAuthenticatedAsync());
    }

    public Lazy<Task<TestUser>> Admin { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Admin:UserIds:0", "1");
    }
}

public class PageViewTests : IClassFixture<AnalyticsApiFactory>, IAsyncLifetime
{
    private readonly AnalyticsApiFactory _factory;
    private readonly HttpClient _anonymous;
    private TestUser _admin = null!;

    public PageViewTests(AnalyticsApiFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Runs before any test creates users, so the admin is user 1.
        _admin = await _factory.Admin.Value;
        Assert.Equal(1, _admin.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<HttpResponseMessage> CountAsync(HttpClient client, string? route) =>
        client.PostAsJsonAsync("/api/analytics/page-views", new PageViewDto { Route = route });

    private async Task<int> ViewsOfAsync(string route)
    {
        var stats = await _admin.Client.GetFromJsonAsync<PageViewStatsDto>("/api/admin/page-views");
        return stats!.PerRoute.SingleOrDefault(r => r.Route == route)?.Views ?? 0;
    }

    [Fact]
    public async Task CountPageView_AddsUpPerRoute_ForVisitorsAndMembers()
    {
        var member = await _factory.CreateAuthenticatedAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await CountAsync(_anonymous, "/person/:personId")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CountAsync(member.Client, "/person/:personId")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CountAsync(_anonymous, "/contact")).StatusCode);

        var stats = await _admin.Client.GetFromJsonAsync<PageViewStatsDto>("/api/admin/page-views");
        Assert.Equal(30, stats!.Days);
        Assert.Equal(2, stats.PerRoute.Single(r => r.Route == "/person/:personId").Views);
        Assert.Equal(1, stats.PerRoute.Single(r => r.Route == "/contact").Views);
        var today = Assert.Single(stats.PerDay, d => d.Day == DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(stats.TotalViews, stats.PerDay.Sum(d => d.Views));
        Assert.True(today.Views >= 3);
    }

    [Fact]
    public async Task CountPageView_IgnoresAddressesThatAreNotRoutes()
    {
        // A real address with an id, and a made-up path: neither is stored.
        Assert.Equal(HttpStatusCode.NoContent, (await CountAsync(_anonymous, "/user/42")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CountAsync(_anonymous, "/spam")).StatusCode);

        var stored = await _factory.WithContextAsync(context =>
            Task.FromResult(context.PageViews.Count(v => v.Route == "/user/42" || v.Route == "/spam")));
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task CountPageView_WithoutRoute_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await CountAsync(_anonymous, null)).StatusCode);
    }

    [Fact]
    public async Task CountPageView_SimultaneousFirstViews_AreAllCounted()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => CountAsync(_factory.CreateClient(), "/terms")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(10, await ViewsOfAsync("/terms"));
    }

    [Fact]
    public async Task GetPageViews_IsForAdminsOnly()
    {
        var member = await _factory.CreateAuthenticatedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/api/admin/page-views")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/admin/page-views")).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task GetPageViews_DaysOutOfRange_Returns400(int days)
    {
        var response = await _admin.Client.GetAsync($"/api/admin/page-views?days={days}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class PageViewRateLimitTests : IClassFixture<LowWriteLimitApiFactory>
{
    private readonly LowWriteLimitApiFactory _factory;

    public PageViewRateLimitTests(LowWriteLimitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PageViews_DoNotUseUpTheWriteLimit()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < LowWriteLimitApiFactory.Limit * 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/analytics/page-views", new PageViewDto { Route = "/" });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
