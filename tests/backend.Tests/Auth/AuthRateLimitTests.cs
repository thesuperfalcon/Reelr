using System.Net;
using System.Net.Http.Json;
using backend.Tests.Infrastructure;

namespace backend.Tests.Auth;

public class LowAuthLimitApiFactory : ReelrApiFactory
{
    public const int Limit = 3;

    protected override int AuthPermitLimit => Limit;
}

// Login and register have separate limits, so each test uses up only its own.
public class AuthRateLimitTests : IClassFixture<LowAuthLimitApiFactory>
{
    private readonly HttpClient _client;

    public AuthRateLimitTests(LowAuthLimitApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private Task<HttpResponseMessage> LoginAsync() =>
        _client.PostAsJsonAsync("/auth/login", new { UserInput = "nobody", Password = TestUsers.Password });

    private Task<HttpResponseMessage> RegisterAsync()
    {
        var username = $"user_{Guid.NewGuid():N}"[..20];
        return _client.PostAsJsonAsync("/auth/register", new { Username = username, Email = $"{username}@test.local", TestUsers.Password });
    }

    [Fact]
    public async Task Login_OverLimit_Returns429()
    {
        for (var i = 0; i < LowAuthLimitApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync()).StatusCode);
        }

        var response = await LoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Register_OverLimit_Returns429()
    {
        for (var i = 0; i < LowAuthLimitApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await RegisterAsync()).StatusCode);
        }

        var response = await RegisterAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
