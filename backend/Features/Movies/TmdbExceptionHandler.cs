using Microsoft.AspNetCore.Diagnostics;

namespace backend.Features.Movies;

// Answers every endpoint that needs TMDB the same way when TMDB is down, instead of a 500.
// The client gets a problem body without TMDB's own error text, which only goes to the log.
public class TmdbExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public TmdbExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TmdbUnavailableException tmdbException)
        {
            return false;
        }

        httpContext.Response.StatusCode = tmdbException.StatusCode;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = tmdbException.StatusCode,
                Title = "Film data is unavailable.",
                Detail = "The film database (TMDB) is not answering right now. Try again in a moment."
            }
        });
    }
}
