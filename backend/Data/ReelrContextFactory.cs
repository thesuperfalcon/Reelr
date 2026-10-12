using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Data;

/// <summary>
/// How the EF tools (dotnet ef, and the migrations bundle the deploy runs) create the context. Without it they
/// would start the whole app, whose start-up checks refuse to run without the JWT key, the TMDB token and the
/// mail settings, none of which a migration needs. The connection string comes from the usual places, or from
/// the bundle's --connection argument.
/// </summary>
public class ReelrContextFactory : IDesignTimeDbContextFactory<ReelrContext>
{
    public ReelrContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<ReelrContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ReelrContext");
        var options = new DbContextOptionsBuilder<ReelrContext>();

        // Without a connection string here, --connection sets it before anything touches the database.
        if (string.IsNullOrEmpty(connectionString))
        {
            options.UseSqlServer();
        }
        else
        {
            options.UseSqlServer(connectionString);
        }

        return new ReelrContext(options.Options);
    }
}
