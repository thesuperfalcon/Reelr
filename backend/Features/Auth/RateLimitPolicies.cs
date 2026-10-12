using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Features.Auth;

public static class RateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Register = "auth-register";
    public const string AccountEmail = "auth-account-email";
    public const string Search = "search";

    /// <summary>
    /// Request limits, all set under "RateLimiting:{name}" (PermitLimit, WindowSeconds):
    /// <list type="bullet">
    /// <item>Read and Write apply to every request, per signed-in user or per IP for anonymous callers.
    /// Writes (POST, PUT, PATCH, DELETE) get a lower limit, which stops spam such as mass comments or follows.</item>
    /// <item>Search applies on top to the search endpoints, since each search calls TMDB.</item>
    /// <item>Login and Register apply per IP, to slow down password guessing and mass sign-ups.</item>
    /// <item>AccountEmail applies per IP to the forms that send an account email (forgot password, resend confirmation).</item>
    /// </list>
    /// Rejected requests get 429 with a Retry-After header.
    /// </summary>
    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var read = Limit(configuration, "Read", permitLimit: 300, windowSeconds: 60);
        var write = Limit(configuration, "Write", permitLimit: 60, windowSeconds: 60);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Docker polls the health check, and it must keep answering however busy the API is.
                if (context.Request.Path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                var isRead = HttpMethods.IsGet(context.Request.Method)
                    || HttpMethods.IsHead(context.Request.Method)
                    || HttpMethods.IsOptions(context.Request.Method);

                return isRead
                    ? RateLimitPartition.GetFixedWindowLimiter($"read:{ClientKey(context)}", _ => read)
                    : RateLimitPartition.GetFixedWindowLimiter($"write:{ClientKey(context)}", _ => write);
            });

            AddPolicy(options, Search, Limit(configuration, "Search", permitLimit: 30, windowSeconds: 60), ClientKey);
            AddPolicy(options, Login, Limit(configuration, "Login", permitLimit: 10, windowSeconds: 60), IpKey);
            AddPolicy(options, Register, Limit(configuration, "Register", permitLimit: 5, windowSeconds: 3600), IpKey);
            AddPolicy(options, AccountEmail, Limit(configuration, "AccountEmail", permitLimit: 5, windowSeconds: 900), IpKey);
        });
    }

    private static FixedWindowRateLimiterOptions Limit(IConfiguration configuration, string name, int permitLimit, int windowSeconds)
    {
        var section = configuration.GetSection($"RateLimiting:{name}");

        return new FixedWindowRateLimiterOptions
        {
            PermitLimit = section.GetValue("PermitLimit", permitLimit),
            Window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", windowSeconds)),
            QueueLimit = 0
        };
    }

    private static void AddPolicy(
        RateLimiterOptions options, string name, FixedWindowRateLimiterOptions limit, Func<HttpContext, string> key)
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(key(context), _ => limit));
    }

    // A signed-in user is limited by account, so people behind one shared IP (a school, an office) do not share a limit.
    private static string ClientKey(HttpContext context) =>
        context.User.FindUserId() is int userId ? $"user:{userId}" : IpKey(context);

    private static string IpKey(HttpContext context) =>
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
