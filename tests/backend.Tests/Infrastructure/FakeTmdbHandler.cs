using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Stands in for the TMDB API so tests never hit the network.
/// Only "movie/{id}" is supported; everything else returns 404.
/// </summary>
public class FakeTmdbHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<int, TmdbMovieDto> _movies = new();
    private readonly ConcurrentDictionary<int, int> _movieRequests = new();

    public void AddMovie(TmdbMovieDto movie) => _movies[movie.Id] = movie;

    public int RequestsForMovie(int tmdbId) => _movieRequests.GetValueOrDefault(tmdbId);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');

        if (segments is [.., "movie", var idText] && int.TryParse(idText, out var tmdbId))
        {
            _movieRequests.AddOrUpdate(tmdbId, 1, (_, count) => count + 1);

            if (_movies.TryGetValue(tmdbId, out var movie))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(movie)
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    // IHttpClientFactory disposes primary handlers when it rotates them,
    // but this instance is shared for the lifetime of the test factory.
    protected override void Dispose(bool disposing)
    {
    }
}
