using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Stands in for the TMDB API so tests never hit the network.
/// Movies added with AddMovie answer "movie/{id}". Any other path can be set with SetResponse.
/// Unknown paths return 404.
/// </summary>
public class FakeTmdbHandler : HttpMessageHandler
{
    private const string ApiVersionPrefix = "/3/";

    private readonly ConcurrentDictionary<int, TmdbMovieDto> _movies = new();
    private readonly ConcurrentDictionary<int, int> _movieRequests = new();
    private readonly ConcurrentDictionary<string, (HttpStatusCode Status, object? Body)> _responses = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _delays = new();
    private readonly ConcurrentDictionary<string, int> _failuresLeft = new();
    private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();

    /// <summary>
    /// Every request received, in order.
    /// </summary>
    public IReadOnlyCollection<HttpRequestMessage> Requests => _requests;

    public void AddMovie(TmdbMovieDto movie) => _movies[movie.Id] = movie;

    /// <summary>
    /// Registers a minimal movie and returns its TMDB id.
    /// </summary>
    public int AddMovie(int tmdbId, string title)
    {
        AddMovie(new TmdbMovieDto
        {
            Id = tmdbId,
            Title = title,
            Overview = $"{title} overview",
            ReleaseDate = "1999-03-31",
            Runtime = 136,
            PosterPath = $"/{tmdbId}.jpg"
        });

        return tmdbId;
    }

    public int RequestsForMovie(int tmdbId) => _movieRequests.GetValueOrDefault(tmdbId);

    /// <summary>
    /// Sets the response for a TMDB path without the version prefix and query, e.g. "movie/603/credits".
    /// </summary>
    public void SetResponse(string path, object? body, HttpStatusCode status = HttpStatusCode.OK) =>
        _responses[path] = (status, body);

    /// <summary>
    /// Makes requests to a path wait this long before answering, to test timeouts.
    /// </summary>
    public void SetDelay(string path, TimeSpan delay) => _delays[path] = delay;

    /// <summary>
    /// Answers the next <paramref name="count"/> requests to a path with 503, then answers normally again.
    /// </summary>
    public void FailNext(string path, int count) => _failuresLeft[path] = count;

    /// <summary>
    /// Requests whose path (without the version prefix) equals the given path.
    /// </summary>
    public IEnumerable<Uri> RequestsTo(string path) =>
        _requests.Select(r => r.RequestUri!).Where(u => PathOf(u) == path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);

        var path = PathOf(request.RequestUri!);

        if (_delays.TryGetValue(path, out var delay))
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (_failuresLeft.TryGetValue(path, out var failures) && failures > 0)
        {
            _failuresLeft[path] = failures - 1;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        if (_responses.TryGetValue(path, out var response))
        {
            return new HttpResponseMessage(response.Status)
            {
                Content = response.Body == null ? null : JsonContent.Create(response.Body)
            };
        }

        if (path.Split('/') is ["movie", var idText] && int.TryParse(idText, out var tmdbId))
        {
            _movieRequests.AddOrUpdate(tmdbId, 1, (_, count) => count + 1);

            if (_movies.TryGetValue(tmdbId, out var movie))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(movie)
                };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static string PathOf(Uri uri) =>
        uri.AbsolutePath.StartsWith(ApiVersionPrefix) ? uri.AbsolutePath[ApiVersionPrefix.Length..] : uri.AbsolutePath.TrimStart('/');

    // IHttpClientFactory disposes primary handlers when it rotates them,
    // but this instance is shared for the lifetime of the test factory.
    protected override void Dispose(bool disposing)
    {
    }
}
