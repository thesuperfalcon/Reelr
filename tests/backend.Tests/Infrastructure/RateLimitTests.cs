using System.Net;
using System.Net.Http.Json;

namespace backend.Tests.Infrastructure;

public class LowReadLimitApiFactory : ReelrApiFactory
{
    public const int Limit = 5;

    protected override int ReadPermitLimit => Limit;
}

public class LowWriteLimitApiFactory : ReelrApiFactory
{
    // Each test user costs two anonymous writes (register and login), so this leaves room for a few users.
    public const int Limit = 8;

    protected override int WritePermitLimit => Limit;
}

public class LowSearchLimitApiFactory : ReelrApiFactory
{
    public const int Limit = 3;

    protected override int SearchPermitLimit => Limit;
}

public class ReadRateLimitTests : IClassFixture<LowReadLimitApiFactory>
{
    private readonly LowReadLimitApiFactory _factory;

    public ReadRateLimitTests(LowReadLimitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousReads_OverLimit_Get429WithRetryAfter_ButHealthStillAnswers()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < LowReadLimitApiFactory.Limit; i++)
        {
            await client.GetAsync("/api/users/999999");
        }

        var response = await client.GetAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task SignedInUsers_EachHaveTheirOwnLimit()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();

        for (var i = 0; i < LowReadLimitApiFactory.Limit; i++)
        {
            await alice.Client.GetAsync("/api/settings");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await alice.Client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.Client.GetAsync("/api/settings")).StatusCode);
    }
}

public class WriteRateLimitTests : IClassFixture<LowWriteLimitApiFactory>
{
    private readonly LowWriteLimitApiFactory _factory;

    public WriteRateLimitTests(LowWriteLimitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Writes_OverLimit_Get429_WhileReadsStillWork()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();

        // Following and unfollowing over and over, as a spam script would.
        for (var i = 0; i < LowWriteLimitApiFactory.Limit; i++)
        {
            var response = i % 2 == 0
                ? await user.Client.PostAsync($"/api/users/{other.Id}/follow", null)
                : await user.Client.DeleteAsync($"/api/users/{other.Id}/follow");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var overLimit = await user.Client.PostAsync($"/api/users/{other.Id}/follow", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/settings")).StatusCode);
    }
}

public class SearchRateLimitTests : IClassFixture<LowSearchLimitApiFactory>
{
    private readonly LowSearchLimitApiFactory _factory;

    public SearchRateLimitTests(LowSearchLimitApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Searches_OverLimit_Get429_WhileOtherReadsStillWork()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < LowSearchLimitApiFactory.Limit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/search?query=someone")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/users/search?query=someone")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users/999999")).StatusCode);
    }
}
