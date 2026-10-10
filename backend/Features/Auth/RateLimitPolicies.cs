using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Features.Auth;

public static class RateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Register = "auth-register";

    /// <summary>
    /// Limits login and register calls per client IP. Login is limited to slow down password guessing,
    /// register to stop mass sign-ups. "RateLimiting:Login:*" and "RateLimiting:Register:*"
    /// (PermitLimit, WindowSeconds) set the limits.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddPerIpPolicy(options, Login, configuration.GetSection("RateLimiting:Login"), permitLimit: 10, windowSeconds: 60);
            AddPerIpPolicy(options, Register, configuration.GetSection("RateLimiting:Register"), permitLimit: 5, windowSeconds: 3600);
        });
    }

    private static void AddPerIpPolicy(
        RateLimiterOptions options, string name, IConfiguration section, int permitLimit, int windowSeconds)
    {
        var limit = section.GetValue("PermitLimit", permitLimit);
        var window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", windowSeconds));

        options.AddPolicy(name, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = window,
                    QueueLimit = 0
                }));
    }
}
