using System.Net;
using System.Net.Http.Json;
using backend.Features.Invites.DTOs;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests.Invites;

/// <summary>
/// Registration is invite only, as in production. The first user (id 1) is the admin who makes the invites.
/// </summary>
public class InviteOnlyApiFactory : ReelrApiFactory
{
    public InviteOnlyApiFactory()
    {
        Admin = new Lazy<Task<TestUser>>(() => this.CreateAuthenticatedAsync());
    }

    public Lazy<Task<TestUser>> Admin { get; }

    protected override bool InviteOnly => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Admin:UserIds:0", "1");
    }
}

public class InviteTests : IClassFixture<InviteOnlyApiFactory>, IAsyncLifetime
{
    private readonly InviteOnlyApiFactory _factory;
    private readonly HttpClient _anonymous;
    private TestUser _admin = null!;

    public InviteTests(InviteOnlyApiFactory factory)
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

    private Task<HttpResponseMessage> RegisterAsync(string? inviteCode, string? username = null)
    {
        username ??= $"user_{Guid.NewGuid():N}"[..20];
        return _anonymous.PostAsJsonAsync("/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = TestUsers.Password,
            AcceptTerms = true,
            InviteCode = inviteCode
        });
    }

    private async Task<CreatedInviteDto> CreateInviteAsync(string? note = null)
    {
        var response = await _admin.Client.PostAsJsonAsync("/api/admin/invites", new CreateInviteDto { Note = note });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedInviteDto>())!;
    }

    private async Task<InviteDto> GetInviteAsync(int id) =>
        (await _admin.Client.GetFromJsonAsync<List<InviteDto>>("/api/admin/invites"))!.Single(i => i.Id == id);

    private static async Task AssertInviteRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("InviteCode", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_WithoutCode_IsRefused()
    {
        await AssertInviteRefusedAsync(await RegisterAsync(null));
    }

    [Fact]
    public async Task Register_WithUnknownCode_IsRefused()
    {
        await AssertInviteRefusedAsync(await RegisterAsync("not-a-real-code"));
    }

    [Fact]
    public async Task CreateInvite_ReturnsCodeAndSignUpLink()
    {
        var invite = await CreateInviteAsync("For Alex");

        Assert.Equal("For Alex", invite.Note);
        Assert.Equal(20, invite.Code.Length);
        Assert.Equal($"http://reelr.test/register?invite={invite.Code}", invite.Link);
    }

    [Fact]
    public async Task Register_WithCode_UsesItOnce()
    {
        var invite = await CreateInviteAsync();
        var username = $"user_{Guid.NewGuid():N}"[..20];

        Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(invite.Code, username)).StatusCode);
        await AssertInviteRefusedAsync(await RegisterAsync(invite.Code));

        var stored = await GetInviteAsync(invite.Id);
        Assert.NotNull(stored.UsedAt);
        Assert.Equal(username, stored.UsedBy);
    }

    [Fact]
    public async Task Register_WithTakenUsername_KeepsTheCodeUsable()
    {
        var invite = await CreateInviteAsync();
        var taken = await _factory.CreateAuthenticatedAsync();

        var refused = await RegisterAsync(invite.Code, taken.Username);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync(invite.Code)).StatusCode);
    }

    [Fact]
    public async Task RevokedInvite_NoLongerWorks()
    {
        var invite = await CreateInviteAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.Client.DeleteAsync($"/api/admin/invites/{invite.Id}")).StatusCode);

        await AssertInviteRefusedAsync(await RegisterAsync(invite.Code));
        Assert.NotNull((await GetInviteAsync(invite.Id)).RevokedAt);
    }

    [Fact]
    public async Task RevokeInvite_AlreadyUsed_IsConflict()
    {
        var invite = await CreateInviteAsync();
        await RegisterAsync(invite.Code);

        var response = await _admin.Client.DeleteAsync($"/api/admin/invites/{invite.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InviteEndpoints_AreForAdminsOnly()
    {
        var member = await _factory.CreateAuthenticatedAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/admin/invites")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.Client.PostAsJsonAsync("/api/admin/invites", new CreateInviteDto())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/api/admin/invites")).StatusCode);
    }
}
