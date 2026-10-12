using System.Net;
using System.Net.Http.Json;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace backend.Tests.Users;

/// <summary>The first user created in this factory's database has id 1, which is configured as a demo account.</summary>
public class DemoAccountApiFactory : ReelrApiFactory
{
    public DemoAccountApiFactory()
    {
        Demo = new Lazy<Task<TestUser>>(() => this.CreateAuthenticatedAsync());
    }

    public Lazy<Task<TestUser>> Demo { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Demo:UserIds:0", "1");
    }
}

public class DemoAccountTests : IClassFixture<DemoAccountApiFactory>, IAsyncLifetime
{
    private readonly DemoAccountApiFactory _factory;
    private TestUser _demo = null!;

    public DemoAccountTests(DemoAccountApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Runs before any test creates users, so the demo account is user 1.
        _demo = await _factory.Demo.Value;
        Assert.Equal(1, _demo.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(DemoAccounts.NotAllowed, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Settings_SayItIsADemoAccount()
    {
        var demo = await _demo.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");
        var member = await (await _factory.CreateAuthenticatedAsync()).Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");

        Assert.True(demo!.IsDemo);
        Assert.False(member!.IsDemo);
    }

    [Fact]
    public async Task AccountChanges_AreRefused()
    {
        await AssertRefusedAsync(await _demo.Client.PostAsJsonAsync("/api/settings/password",
            new { CurrentPassword = TestUsers.Password, NewPassword = "N3w-passw0rd!" }));
        await AssertRefusedAsync(await _demo.Client.PatchAsJsonAsync("/api/settings", new { UserName = "taken_over" }));
        await AssertRefusedAsync(await _demo.Client.PostAsync("/api/settings/logout-everywhere", null));
        await AssertRefusedAsync(await _demo.Client.DeleteAsync("/api/settings/avatar"));
        await AssertRefusedAsync(await _demo.Client.DeleteAccountAsync(_demo.Id));

        // Still the same account, still signed in.
        var settings = await _demo.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");
        Assert.Equal(_demo.Username, settings!.UserName);
    }

    [Fact]
    public async Task PrivacySettings_CanChange()
    {
        var response = await _demo.Client.PatchAsJsonAsync("/api/settings", new { ShowFriendReviews = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MemberFeatures_Work()
    {
        var tmdbId = _factory.Tmdb.AddMovie(7001, "Demo film");

        var response = await _demo.Client.PostAsync($"/api/watchlist/{tmdbId}", null);

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ForgotPassword_SendsNoEmail()
    {
        var address = $"{_demo.Username}@test.local";
        var member = await _factory.CreateAuthenticatedAsync();
        var memberAddress = $"{member.Username}@test.local";
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { Email = address });
        await client.PostAsJsonAsync("/auth/forgot-password", new { Email = memberAddress });

        // Emails go out in order, so once the member's has arrived, the demo account's would have too.
        await _factory.Emails.WaitForAsync(memberAddress, "Reset your Reelr password");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(_factory.Emails.SentTo(address), m => m.Subject.Contains("Reset your Reelr password"));
    }

    [Fact]
    public async Task OtherMembers_CanStillChangeTheirAccount()
    {
        var member = await _factory.CreateAuthenticatedAsync();

        var response = await member.Client.PatchAsJsonAsync("/api/settings", new { UserName = $"renamed_{member.Id}" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
