using backend.Features.Auth;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace backend.Features.Observability;

public static class Logging
{
    /// <summary>
    /// Serilog writes every log line to the console: readable text in Development, one JSON object per line
    /// everywhere else, so a log collector can search by field. Levels come from the "Serilog" section
    /// (for example Serilog__MinimumLevel__Default=Debug). Other logger providers, such as the one the tests
    /// add, still get every event. The logger belongs to this app, not to Serilog's static Log.Logger,
    /// so two hosts in one process (as in the tests) keep their logs apart.
    /// </summary>
    public static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
    {
        var json = !builder.Environment.IsDevelopment();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            if (json)
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                logger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            }
        }, preserveStaticLogger: true, writeToProviders: true);

        return builder;
    }

    /// <summary>
    /// One line per request with method, path, status and time taken, plus the user id when signed in.
    /// The path leaves out the query string, so search text stays out of the log. Health checks are polled
    /// all the time and only logged when they fail.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";

            options.GetLevel = (context, _, exception) =>
                exception != null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
                : context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
                : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                if (context.User.FindUserId() is int userId)
                {
                    diagnostics.Set("UserId", userId);
                }
            };
        });
}
