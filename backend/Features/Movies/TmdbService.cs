using System.Net.Http.Headers;
using System.Text.Json;
using backend.Features.Movies.DTOs;

namespace backend.Features.Movies;

public class TmdbService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TmdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<TmdbMovieDto?> GetMovie(int tmdbId)
    {
        var token = _configuration["TMDB_READ_ACCESS_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TMDB_READ_ACCESS_TOKEN saknas.");
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"movie/{tmdbId}?language=en-US"
        );

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"TMDB error: {(int)response.StatusCode} {response.StatusCode}. Response: {error}"
            );
        }

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<TmdbMovieDto>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
    }
    public async Task<TmdbSearchResultDto?> SearchMovies(string query)
    {
        var token = _configuration["TMDB_READ_ACCESS_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "TMDB_READ_ACCESS_TOKEN saknas."
            );
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"search/movie?query={Uri.EscapeDataString(query)}&language=en-US"
        );

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"TMDB error: {(int)response.StatusCode} " +
                $"{response.StatusCode}. Response: {error}"
            );
        }

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<TmdbSearchResultDto>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
    }

    public async Task<TmdbCreditsDto?> GetCredits(int tmdbId)
    {
        var token = _configuration["TMDB_READ_ACCESS_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "TMDB_READ_ACCESS_TOKEN saknas."
            );
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"movie/{tmdbId}/credits?language=en-US"
        );

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"TMDB error: {(int)response.StatusCode} " +
                $"{response.StatusCode}. Response: {error}"
            );
        }

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<TmdbCreditsDto>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
    }
}
