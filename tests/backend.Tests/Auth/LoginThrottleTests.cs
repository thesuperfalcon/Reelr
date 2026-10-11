using System.Net;
using System.Net.Http.Json;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Auth;

public class LowLoginThrottleApiFactory : ReelrApiFactory
{
    public const int Limit = 3;

    protected override int LoginPerAccountLimit => Limit;
}

public class LoginThrottleTests : IClassFixture<LowLoginThrottleApiFactory>
{
    private readonly LowLoginThrottleApiFactory _factory;
    private readonly HttpClient _client;

    public LoginThrottleTests(LowLoginThrottleApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<HttpResponseMessage> LoginAsync(string userInput, string password = TestUsers.Password) =>
        _client.PostAsJsonAsync("/auth/login", new { UserInput = userInput, Password = password });

    private async Task<string> RegisterAsync()
    {
        var username = $"user_{Guid.NewGuid():N}"[..20];
        (await _client.PostAsJsonAsync("/auth/register", new { Username = username, Email = $"{username}@test.local", TestUsers.Password }))
            .EnsureSuccessStatusCode();
        await _factory.ConfirmEmailAsync(username);
        return username;
    }

    [Fact]
    public async Task WrongPasswordsFromOneAddress_BlockThatAddress_WithoutLockingTheAccount()
    {
        var username = await RegisterAsync();

        for (var i = 0; i < LowLoginThrottleApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(username, "WrongPassw0rd!")).StatusCode);
        }

        // Even the right password is refused from this address, so guessing cannot go on here.
        var blocked = await LoginAsync(username);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);

        // The account itself is not locked, so its owner can still sign in from another address.
        var user = await _factory.WithContextAsync(context => context.Users.SingleAsync(u => u.UserName == username));
        Assert.Null(user.LockoutEnd);
        Assert.Equal(LowLoginThrottleApiFactory.Limit, user.AccessFailedCount);
    }

    [Fact]
    public async Task BlockedAccount_DoesNotAffectOtherAccountsFromTheSameAddress()
    {
        var target = await RegisterAsync();
        var other = await RegisterAsync();

        for (var i = 0; i < LowLoginThrottleApiFactory.Limit; i++)
        {
            await LoginAsync(target, "WrongPassw0rd!");
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(other)).StatusCode);
    }

    [Fact]
    public async Task UnknownName_IsThrottledLikeARealOne()
    {
        // Otherwise a 429 would tell which names belong to real accounts.
        var unknown = $"nobody_{Guid.NewGuid():N}"[..20];

        for (var i = 0; i < LowLoginThrottleApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(unknown)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(unknown)).StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogin_ClearsTheCount()
    {
        var username = await RegisterAsync();

        for (var i = 0; i < LowLoginThrottleApiFactory.Limit - 1; i++)
        {
            await LoginAsync(username, "WrongPassw0rd!");
        }
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username)).StatusCode);

        for (var i = 0; i < LowLoginThrottleApiFactory.Limit - 1; i++)
        {
            await LoginAsync(username, "WrongPassw0rd!");
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username)).StatusCode);
    }
}
