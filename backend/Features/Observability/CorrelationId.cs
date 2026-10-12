using Serilog.Context;

namespace backend.Features.Observability;

/// <summary>
/// Gives every request one id that ties together all its log lines, the nginx access log line and the
/// error a user sees. nginx sends its own request id in X-Correlation-Id; without one (such as in
/// development) the app makes one. The id goes back in the response header, on every log line of the
/// request as CorrelationId, and into error responses as correlationId, so a user can quote it.
/// </summary>
public static class CorrelationId
{
    public const string Header = "X-Correlation-Id";
    public const string LogProperty = "CorrelationId";
    private const int MaxLength = 64;

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var id = Read(context.Request.Headers[Header].ToString()) ?? Guid.NewGuid().ToString("N");

            context.TraceIdentifier = id;

            // Set as the response starts: the exception handler clears headers before it writes an error.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[Header] = id;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty(LogProperty, id))
            {
                await next(context);
            }
        });

    // Called for every ProblemDetails response (AddProblemDetails in Program.cs).
    public static void AddToProblem(ProblemDetailsContext context) =>
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

    // An id from the caller is kept only if it is short and plain, so it cannot break a log line.
    private static string? Read(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? value
            : null;
}
