using System.Text.Json;
using backend.Data;
using backend.Features.Movies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace backend.Features.Observability;

/// <summary>
/// Two health endpoints:
/// <list type="bullet">
/// <item>GET /health/live answers 200 as long as the process runs. A restart is the fix when it does not.</item>
/// <item>GET /health/ready checks what requests need: 503 when the database does not answer, so a load
/// balancer or Docker holds traffic back. TMDB being down only marks it Degraded (still 200), since most
/// of the app works from the database and a restart would not bring TMDB back.</item>
/// </list>
/// Both answer with a small JSON body of each check's status, without error details.
/// </summary>
public static class HealthChecks
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ReelrContext>("database", tags: [ReadyTag])
            .AddCheck<TmdbHealthCheck>("tmdb", failureStatus: HealthStatus.Degraded, tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteAsync
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteAsync
        });

        return endpoints;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        }));
    }
}

/// <summary>
/// Whether TMDB answers. The result is kept for a minute, so frequent probes do not use up the TMDB quota.
/// </summary>
public class TmdbHealthCheck : IHealthCheck
{
    private const string CacheKey = "health:tmdb";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private readonly TmdbService _tmdb;
    private readonly IMemoryCache _cache;

    public TmdbHealthCheck(TmdbService tmdb, IMemoryCache cache)
    {
        _tmdb = tmdb;
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var answers = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;

            try
            {
                return await _tmdb.PingAsync(cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or Polly.ExecutionRejectedException)
            {
                return false;
            }
        });

        return answers
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "TMDB does not answer.");
    }
}
