using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Features.Auth;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Auth;

/// <summary>
/// Tokens stop working before they expire when the security stamp changes: on a password change,
/// "log out everywhere", a rename, or when the account is deleted.
/// </summary>
public class TokenRevocationTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public TokenRevocationTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAgainAsync(TestUser user, string password = TestUsers.Password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenDto>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherDevices_AndReturnsANewTokenForThisOne()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var otherDevice = await LoginAgainAsync(user);

        var response = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = TestUsers.Password, NewPassword = TestUsers.Password + "-new" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newToken = (await response.Content.ReadFromJsonAsync<TokenDto>())!.Token;

        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/settings")).StatusCode);

        var thisDevice = _factory.CreateClient();
        thisDevice.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.OK, (await thisDevice.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task LogoutEverywhere_RevokesEveryToken()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var otherDevice = await LoginAgainAsync(user);

        var response = await user.Client.PostAsync("/api/settings/logout-everywhere", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/settings")).StatusCode);
        // Logging in again works as usual.
        Assert.Equal(HttpStatusCode.OK, (await (await LoginAgainAsync(user)).GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task DeletedAccount_TokenStopsWorking()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var otherDevice = await LoginAgainAsync(user);

        (await user.Client.DeleteAccountAsync(user.Id)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/lists")).StatusCode);
    }

    [Fact]
    public async Task TokenWithAnOldStamp_IsRefused()
    {
        // Tokens issued before stamps existed carry an empty one; they must log in again once.
        var user = await _factory.CreateAuthenticatedAsync();
        var token = await _factory.WithContextAsync(async context =>
        {
            var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
            return _factory.Services.GetRequiredService<TokenService>()
                .CreateToken(new User { Id = stored.Id, UserName = stored.UserName, SecurityStamp = null });
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/settings")).StatusCode);
    }
}
