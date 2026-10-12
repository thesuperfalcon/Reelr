using System.Net;
using System.Net.Http.Json;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Auth;

/// <summary>
/// Low auth limits, with every request arriving from <see cref="ClientAddress"/>, a public address.
/// The test server has no real connection, so a startup filter sets the address before the app's middleware runs.
/// </summary>
public class PublicClientApiFactory : LowAuthLimitApiFactory
{
    public static readonly IPAddress ClientAddress = IPAddress.Parse("203.0.113.10");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ClientAddressFilter>());
    }

    private sealed class ClientAddressFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = ClientAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>The same, but the client's network is configured as a trusted proxy, like nginx in Docker.</summary>
public class TrustedProxyApiFactory : PublicClientApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "203.0.113.0/24");
    }
}

public class UntrustedForwardedHeadersTests : IClassFixture<PublicClientApiFactory>
{
    private readonly HttpClient _client;

    public UntrustedForwardedHeadersTests(PublicClientApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_FakedForwardedFor_DoesNotGetPastTheLimit()
    {
        HttpResponseMessage response = null!;

        // A client calling the API directly makes up a new address for every request.
        for (var i = 0; i <= LowAuthLimitApiFactory.Limit; i++)
        {
            response = await ForwardedLoginAsync(_client, $"198.51.100.{i + 1}");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    internal static Task<HttpResponseMessage> ForwardedLoginAsync(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { UserInput = "nobody", Password = TestUsers.Password })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }
}

public class TrustedForwardedHeadersTests : IClassFixture<TrustedProxyApiFactory>
{
    private readonly HttpClient _client;

    public TrustedForwardedHeadersTests(TrustedProxyApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ThroughTrustedProxy_IsLimitedPerForwardedClient()
    {
        for (var i = 0; i < LowAuthLimitApiFactory.Limit; i++)
        {
            await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, "198.51.100.1");
        }

        var sameClient = await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, "198.51.100.1");
        var otherClient = await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, "198.51.100.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, sameClient.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherClient.StatusCode);
    }
}

/// <summary>
/// Two trusted proxies, like Azure Container Apps: the platform's proxy and nginx both add an address.
/// The request arrives from the first proxy (203.0.113.10); the second one is in 192.0.2.0/24.
/// </summary>
public class TrustedProxyChainApiFactory : PublicClientApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", "203.0.113.0/24");
        builder.UseSetting("ForwardedHeaders:KnownNetworks:1", "192.0.2.0/24");
        builder.UseSetting("ForwardedHeaders:ForwardLimit", "2");
    }
}

public class TrustedProxyChainTests : IClassFixture<TrustedProxyChainApiFactory>
{
    private readonly HttpClient _client;

    public TrustedProxyChainTests(TrustedProxyChainApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ThroughTwoProxies_IsLimitedPerClientEvenWithAFakedAddress()
    {
        // The client puts a new made-up address in front each time; the proxies add the real one and their own.
        for (var i = 0; i < LowAuthLimitApiFactory.Limit; i++)
        {
            await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, $"198.18.0.{i + 1}, 198.51.100.1, 192.0.2.5");
        }

        var sameClient = await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, "198.18.0.99, 198.51.100.1, 192.0.2.5");
        var otherClient = await UntrustedForwardedHeadersTests.ForwardedLoginAsync(_client, "198.51.100.2, 192.0.2.5");

        Assert.Equal(HttpStatusCode.TooManyRequests, sameClient.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherClient.StatusCode);
    }
}
