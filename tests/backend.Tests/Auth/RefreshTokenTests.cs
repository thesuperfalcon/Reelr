using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Data;
using backend.Features.Auth;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Auth;

/// <summary>
/// Once held, the next query on the refresh tokens waits until it is cancelled, like a paused database that is
/// still waking up. The wait ends by itself after a while, so a request that is never cancelled cannot hang the run.
/// </summary>
public class HeldRefreshTokenQuery : DbCommandInterceptor
{
    private int _held;

    public TaskCompletionSource Reached { get; private set; } = new();

    public TaskCompletionSource Cancelled { get; private set; } = new();

    public void HoldNext()
    {
        Reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _held, 1);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("[RefreshTokens]") && Interlocked.Exchange(ref _held, 0) == 1)
        {
            Reached.SetResult();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled.SetResult();
                throw;
            }
        }

        return result;
    }
}

public class RefreshTokenApiFactory : ReelrApiFactory
{
    public HeldRefreshTokenQuery HeldQuery { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.ConfigureDbContext<ReelrContext>(options => options.AddInterceptors(HeldQuery)));
    }
}

public class RefreshTokenTests : IClassFixture<RefreshTokenApiFactory>
{
    private readonly RefreshTokenApiFactory _factory;

    public RefreshTokenTests(RefreshTokenApiFactory factory)
    {
        _factory = factory;
    }

    // Cookies are handled by hand, so a test can send an old cookie again the way a thief would.
    private HttpClient Client() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private static string? RefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshTokens.CookieName + "="))
            : null;
        var value = header?.Split(';')[0][(RefreshTokens.CookieName.Length + 1)..];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private async Task<(TestUser User, string Cookie)> LoginAsync()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var response = await Client().PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = TestUsers.Password });
        response.EnsureSuccessStatusCode();
        return (user, RefreshCookie(response)!);
    }

    private Task<HttpResponseMessage> RefreshAsync(string? cookie, bool csrfHeader = true, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        if (cookie != null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokens.CookieName}={cookie}");
        }
        if (csrfHeader)
        {
            request.Headers.Add(RefreshTokens.CsrfHeader, "1");
        }
        return Client().SendAsync(request, cancellationToken);
    }

    // Moves every used token back in time, past the window in which two tabs may share one.
    private Task<int> MarkUsedTokensLongAgoAsync() => _factory.WithContextAsync(context =>
        context.RefreshTokens
            .Where(t => t.UsedAt != null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, DateTime.UtcNow.AddMinutes(-5))));

    [Fact]
    public async Task Login_SetsAProtectedRefreshCookie()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await Client().PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = TestUsers.Password });

        var header = response.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(RefreshTokens.CookieName));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/auth", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_GivesAWorkingAccessTokenAndANewCookie()
    {
        var (_, cookie) = await LoginAsync();

        var response = await RefreshAsync(cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newCookie = RefreshCookie(response);
        Assert.NotNull(newCookie);
        Assert.NotEqual(cookie, newCookie);
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
        var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutTheCsrfHeader_Returns403()
    {
        var (_, cookie) = await LoginAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await RefreshAsync(cookie, csrfHeader: false)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutOrWithAnUnknownCookie_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("made-up-token")).StatusCode);
    }

    [Fact]
    public async Task Refresh_SameCookieTwiceAtOnce_BothWork()
    {
        var (_, cookie) = await LoginAsync();

        var first = await RefreshAsync(cookie);
        var second = await RefreshAsync(cookie);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Refresh_UsedCookieComingBackLater_EndsTheWholeSignIn()
    {
        var (user, stolen) = await LoginAsync();
        var current = RefreshCookie(await RefreshAsync(stolen))!;
        await MarkUsedTokensLongAgoAsync();

        var replay = await RefreshAsync(stolen);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        // The rightful device's newer cookie is from the same sign-in, so it stops working too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(current)).StatusCode);
        Assert.Contains(_factory.Logs.Entries, e => e.EventId.Id == 1013 && Equals(e.State.GetValueOrDefault("UserId"), user.Id));
    }

    [Fact]
    public async Task Refresh_DroppedByTheClientWhileTheDatabaseWakes_KeepsTheOldCookieValid()
    {
        var (_, cookie) = await LoginAsync();
        _factory.HeldQuery.HoldNext();
        using var phone = new CancellationTokenSource();

        // The phone leaves the app while the database is still waking up.
        var dropped = RefreshAsync(cookie, cancellationToken: phone.Token);
        await _factory.HeldQuery.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await phone.CancelAsync();

        // The server stops waiting too, instead of rotating the token for a client that is gone.
        await _factory.HeldQuery.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dropped);
        // Back in the app minutes later, with the cookie it already had.
        await MarkUsedTokensLongAgoAsync();
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(cookie)).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsThisSignInOnly()
    {
        var (user, cookie) = await LoginAsync();
        var otherDevice = RefreshCookie(await Client().PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = TestUsers.Password }))!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", $"{RefreshTokens.CookieName}={cookie}");
        request.Headers.Add(RefreshTokens.CsrfHeader, "1");
        var response = await Client().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(otherDevice)).StatusCode);
    }

    [Fact]
    public async Task PasswordChange_EndsOtherSignInsAndGivesThisDeviceANewCookie()
    {
        var (user, otherDevice) = await LoginAsync();

        var response = await Client().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/settings/password")
        {
            Headers = { Authorization = user.Client.DefaultRequestHeaders.Authorization },
            Content = JsonContent.Create(new { CurrentPassword = TestUsers.Password, NewPassword = "N3w-passw0rd!" })
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(otherDevice)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(RefreshCookie(response))).StatusCode);
    }

    [Fact]
    public async Task LogoutEverywhere_EndsEverySignIn()
    {
        var (user, cookie) = await LoginAsync();

        (await user.Client.PostAsync("/api/settings/logout-everywhere", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(cookie)).StatusCode);
    }

    private record TokenResponse(string Token);
}
