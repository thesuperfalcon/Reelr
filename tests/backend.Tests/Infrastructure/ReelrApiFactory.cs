using backend.Data;
using backend.Features.Movies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway SQL Server database and a fake TMDB.
/// One instance (and one database) is shared by all tests in a class.
/// SQLite is not used because some queries (e.g. Take inside a projection) need SQL APPLY.
/// </summary>
public class ReelrApiFactory : WebApplicationFactory<Program>
{
    // Override with the REELR_TEST_SQLSERVER environment variable, e.g. for LocalDB or a container.
    private const string DefaultServer = "Server=.\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable("REELR_TEST_SQLSERVER") ?? DefaultServer}Database=ReelrTests_{Guid.NewGuid():N};";

    public FakeTmdbHandler Tmdb { get; } = new();

    protected virtual int AuthPermitLimit => 100_000;

    protected virtual int ReadPermitLimit => 100_000;

    protected virtual int WritePermitLimit => 100_000;

    protected virtual int SearchPermitLimit => 100_000;

    // Lifted so the lockout tests can send more wrong passwords than one address normally may.
    protected virtual int LoginPerAccountLimit => 100_000;

    // Most tests change the fake TMDB between calls and check what was sent, so the TMDB cache is off.
    // TmdbCacheTests turns it on.
    protected virtual int TmdbCacheMegabytes => 0;

    // False runs the TMDB client with the defaults in Program.cs, as in production.
    protected virtual bool OverrideTmdbResilience => true;

    /// <summary>
    /// Runs code against the test database directly, for setup the API cannot do yet or for checking stored values.
    /// </summary>
    public async Task<T> WithContextAsync<T>(Func<ReelrContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ReelrContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:ReelrContext", _connectionString);
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-for-hmac-sha256");
        builder.UseSetting("TMDB_READ_ACCESS_TOKEN", "test-token");
        // Tests read the community feed right after changing data, so its short cache is off.
        builder.UseSetting("Activity:CommunityCacheSeconds", "0");
        // Every test registers and logs in from the same address, so the auth limits are lifted.
        // AuthRateLimitTests sets low limits to check the limiter itself.
        builder.UseSetting("RateLimiting:Login:PermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("RateLimiting:Register:PermitLimit", AuthPermitLimit.ToString());
        builder.UseSetting("RateLimiting:LoginPerAccount:PermitLimit", LoginPerAccountLimit.ToString());
        // Anonymous test requests all share one partition, so the general limits are lifted too.
        // RateLimitTests sets low limits to check them.
        builder.UseSetting("RateLimiting:Read:PermitLimit", ReadPermitLimit.ToString());
        builder.UseSetting("RateLimiting:Write:PermitLimit", WritePermitLimit.ToString());
        builder.UseSetting("RateLimiting:Search:PermitLimit", SearchPermitLimit.ToString());
        builder.UseSetting("Tmdb:CacheSizeMegabytes", TmdbCacheMegabytes.ToString());
        // Retries without waiting and short timeouts keep the TMDB failure tests fast. The circuit breaker
        // would otherwise open once a test class has sent enough failing requests and affect later tests.
        if (OverrideTmdbResilience)
        {
            builder.UseSetting("Tmdb:Resilience:Retry:Delay", "00:00:00");
            builder.UseSetting("Tmdb:Resilience:AttemptTimeout:Timeout", "00:00:00.5");
            builder.UseSetting("Tmdb:Resilience:TotalRequestTimeout:Timeout", "00:00:02");
            builder.UseSetting("Tmdb:Resilience:CircuitBreaker:MinimumThroughput", "100000");
        }

        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<TmdbService>()
                .ConfigurePrimaryHttpMessageHandler(() => Tmdb);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ReelrContext>().Database.EnsureCreated();

        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ReelrContext>().Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
