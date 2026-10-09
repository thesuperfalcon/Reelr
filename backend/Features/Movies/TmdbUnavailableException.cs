namespace backend.Features.Movies;

// TMDB could not give an answer: it failed, sent something unreadable, was too slow, or is being skipped
// for a while after many failures. TmdbExceptionHandler turns it into a 502 or 503 for the client.
public class TmdbUnavailableException : Exception
{
    public TmdbUnavailableException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    // 502 when TMDB answered with an error, 503 when it did not answer in time or is being skipped.
    public int StatusCode { get; }
}
