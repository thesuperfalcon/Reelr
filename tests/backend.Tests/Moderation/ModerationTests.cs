using System.Net;
using System.Net.Http.Json;
using backend.Features.Diary.DTOs;
using backend.Features.Moderation;
using backend.Features.Moderation.DTOs;
using backend.Features.MovieLists.DTOs;
using backend.Features.Reviews.DTOs;
using backend.Features.Settings.DTOs;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace backend.Tests.Moderation;

/// <summary>
/// The first user created in this factory's database has id 1, which is configured as the admin.
/// </summary>
public class ModerationApiFactory : ReelrApiFactory
{
    public ModerationApiFactory()
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

public class ModerationTests : IClassFixture<ModerationApiFactory>, IAsyncLifetime
{
    private readonly ModerationApiFactory _factory;
    private TestUser _admin = null!;

    public ModerationTests(ModerationApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Runs before any test creates users, so the admin is user 1.
        _admin = await _factory.Admin.Value;
        Assert.Equal(1, _admin.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ReviewDto> ReviewAsync(TestUser author, int tmdbId, string text = "A review")
    {
        var response = await author.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/reviews", new CreateReviewDto { Text = text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReviewDto>())!;
    }

    private static Task<HttpResponseMessage> ReportAsync(TestUser reporter, ReportTargetType type, int id, ReportReason reason = ReportReason.Spam, string? note = null) =>
        reporter.Client.PostAsJsonAsync("/api/reports", new CreateReportDto { TargetType = type, TargetId = id, Reason = reason, Note = note });

    private async Task<List<ReportedItemDto>> OpenReportsAsync() =>
        (await _admin.Client.GetFromJsonAsync<List<ReportedItemDto>>("/api/admin/reports"))!;

    [Fact]
    public async Task ReportedReview_ShowsForAdmin_GroupedWithItsReasons()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var first = await _factory.CreateAuthenticatedAsync();
        var second = await _factory.CreateAuthenticatedAsync();
        var review = await ReviewAsync(author, _factory.Tmdb.AddMovie(9501, "Reported film"), "Buy cheap watches at example.test");

        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(first, ReportTargetType.Review, review.Id, ReportReason.Spam, "Advert")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(second, ReportTargetType.Review, review.Id, ReportReason.Inappropriate)).StatusCode);

        var item = Assert.Single(await OpenReportsAsync(), i => i.TargetType == ReportTargetType.Review && i.TargetId == review.Id);
        Assert.True(item.Exists);
        Assert.Equal(2, item.ReportCount);
        Assert.Equal(author.Id, item.Author!.Id);
        Assert.Contains("cheap watches", item.Text);
        Assert.Equal("Reported film", item.Context);
        Assert.Equal([ReportReason.Spam, ReportReason.Inappropriate], item.Reasons.Order());
        Assert.Equal(["Advert"], item.Notes);
    }

    [Fact]
    public async Task AdminEndpoints_AreOnlyForAdmins()
    {
        var member = await _factory.CreateAuthenticatedAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/admin/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PostAsJsonAsync("/api/admin/reports/resolve",
            new ResolveReportsDto { TargetType = ReportTargetType.Review, TargetId = 1, Outcome = ReportOutcome.Removed })).StatusCode);

        Assert.True((await _admin.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings"))!.IsAdmin);
        Assert.False((await member.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings"))!.IsAdmin);
    }

    [Fact]
    public async Task Report_OwnContent_Twice_Missing_OrPrivateList_IsRefused()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reporter = await _factory.CreateAuthenticatedAsync();
        var review = await ReviewAsync(author, _factory.Tmdb.AddMovie(9502, "Refused"));
        var privateList = (await (await author.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "Secret", IsPublic = false }))
            .Content.ReadFromJsonAsync<MovieListDto>())!;

        Assert.Equal(HttpStatusCode.BadRequest, (await ReportAsync(author, ReportTargetType.Review, review.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(reporter, ReportTargetType.Review, review.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReportAsync(reporter, ReportTargetType.Review, review.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(reporter, ReportTargetType.Comment, 999999)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(reporter, ReportTargetType.List, privateList.Id)).StatusCode);
    }

    [Fact]
    public async Task Resolve_Removed_DeletesTheReview_AndClosesItsReports()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var reporter = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9503, "Removed");
        var review = await ReviewAsync(author, tmdbId);
        await ReportAsync(reporter, ReportTargetType.Review, review.Id, ReportReason.Harassment);

        var response = await _admin.Client.PostAsJsonAsync("/api/admin/reports/resolve",
            new ResolveReportsDto { TargetType = ReportTargetType.Review, TargetId = review.Id, Outcome = ReportOutcome.Removed });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync($"/api/reviews/{review.Id}")).StatusCode);
        Assert.DoesNotContain(await OpenReportsAsync(), i => i.TargetId == review.Id && i.TargetType == ReportTargetType.Review);
        // The reporter may report the same kind of thing again later; the old report is closed.
        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(reporter, ReportTargetType.Review, review.Id)).StatusCode);
    }

    [Fact]
    public async Task Resolve_Dismissed_KeepsTheComment()
    {
        var author = await _factory.CreateAuthenticatedAsync();
        var commenter = await _factory.CreateAuthenticatedAsync();
        var reporter = await _factory.CreateAuthenticatedAsync();
        var review = await ReviewAsync(author, _factory.Tmdb.AddMovie(9504, "Dismissed"));
        var comment = (await (await commenter.Client.PostAsJsonAsync($"/api/reviews/{review.Id}/comments", new CreateReviewCommentDto { Text = "Fair point" }))
            .Content.ReadFromJsonAsync<ReviewCommentDto>())!;
        await ReportAsync(reporter, ReportTargetType.Comment, comment.Id, ReportReason.Other, "I just disagree");

        var response = await _admin.Client.PostAsJsonAsync("/api/admin/reports/resolve",
            new ResolveReportsDto { TargetType = ReportTargetType.Comment, TargetId = comment.Id, Outcome = ReportOutcome.Dismissed });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var comments = await _factory.CreateClient().GetFromJsonAsync<List<ReviewCommentDto>>($"/api/reviews/{review.Id}/comments");
        Assert.Single(comments!);
        Assert.DoesNotContain(await OpenReportsAsync(), i => i.TargetId == comment.Id && i.TargetType == ReportTargetType.Comment);
    }

    [Fact]
    public async Task Resolve_WithoutOpenReports_Returns404()
    {
        var response = await _admin.Client.PostAsJsonAsync("/api/admin/reports/resolve",
            new ResolveReportsDto { TargetType = ReportTargetType.List, TargetId = 987654, Outcome = ReportOutcome.Dismissed });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
