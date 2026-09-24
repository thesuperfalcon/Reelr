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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:ReelrContext", _connectionString);
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-long-enough-for-hmac-sha256");
        builder.UseSetting("TMDB_READ_ACCESS_TOKEN", "test-token");

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
