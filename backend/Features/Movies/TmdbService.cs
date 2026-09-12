using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.Features.Movies.DTOs;

namespace backend.Features.Movies;

public class TmdbService
{
    private const int MaxCastMembers = 10;
    private const int MaxImages = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> RelevantCrewJobs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Director",
        "Writer",
        "Screenplay",
        "Story",
        "Producer",
        "Executive Producer",
        "Cinematography",
        "Editor",
        "Original Music Composer"
    };

    private static readonly HashSet<string> TrailerVideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trailer",
        "Teaser"
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TmdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public Task<TmdbMovieDto?> GetMovie(int tmdbId) =>
        GetFromTmdb<TmdbMovieDto>($"movie/{tmdbId}?language=en-US", notFoundReturnsNull: true);

    public Task<TmdbSearchResultDto?> SearchMovies(string query) =>
        GetFromTmdb<TmdbSearchResultDto>($"search/movie?query={Uri.EscapeDataString(query)}&language=en-US");

    public Task<TmdbSearchResultDto?> DiscoverMovies(int? castId, int? crewId, int? studioId, int? genreId)
    {
        var queryParams = new List<string> { "language=en-US" };

        if (castId.HasValue)
        {
            queryParams.Add($"with_cast={castId.Value}");
        }

        if (crewId.HasValue)
        {
            queryParams.Add($"with_crew={crewId.Value}");
        }

        if (studioId.HasValue)
        {
            queryParams.Add($"with_companies={studioId.Value}");
        }

        if (genreId.HasValue)
        {
            queryParams.Add($"with_genres={genreId.Value}");
        }

        return GetFromTmdb<TmdbSearchResultDto>($"discover/movie?{string.Join("&", queryParams)}");
    }

    public Task<TmdbSearchResultDto?> GetRecommendedMovies(int tmdbId) =>
        GetFromTmdb<TmdbSearchResultDto>($"movie/{tmdbId}/recommendations?language=en-US", notFoundReturnsNull: true);

    // TMDB note: this method only looks for other items based on genres and plot keywords,
    // so results are not always going to be accurate. Use it with that in mind.
    public Task<TmdbSearchResultDto?> GetSimilarMovies(int tmdbId) =>
        GetFromTmdb<TmdbSearchResultDto>($"movie/{tmdbId}/similar?language=en-US", notFoundReturnsNull: true);

    public Task<TmdbSearchResultDto?> GetTrendingMovies() =>
        GetFromTmdb<TmdbSearchResultDto>("trending/movie/week?language=en-US");

    public Task<TmdbSearchResultDto?> GetPopularMovies() =>
        GetFromTmdb<TmdbSearchResultDto>("movie/popular?language=en-US");

    public Task<TmdbCreditsDto?> GetCredits(int tmdbId) =>
        GetFromTmdb<TmdbCreditsDto>($"movie/{tmdbId}/credits?language=en-US", notFoundReturnsNull: true);

    public Task<TmdbVideosDto?> GetVideos(int tmdbId) =>
        GetFromTmdb<TmdbVideosDto>($"movie/{tmdbId}/videos?language=en-US");

    public Task<TmdbImagesDto?> GetImages(int tmdbId) =>
        GetFromTmdb<TmdbImagesDto>($"movie/{tmdbId}/images");

    public async Task<MovieDetailsDto?> GetMovieDetails(int tmdbId)
    {
        var movie = await GetMovie(tmdbId);

        if (movie == null)
        {
            return null;
        }

        var creditsTask = TryGetCredits(tmdbId);
        var videosTask = TryGetVideos(tmdbId);
        var imagesTask = TryGetImages(tmdbId);

        await Task.WhenAll(creditsTask, videosTask, imagesTask);

        var credits = await creditsTask;
        var videos = await videosTask;
        var images = await imagesTask;

        return new MovieDetailsDto
        {
            Id = movie.Id,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            Overview = movie.Overview,
            Tagline = movie.Tagline,
            ReleaseDate = movie.ReleaseDate,
            Runtime = movie.Runtime,
            PosterPath = movie.PosterPath,
            BackdropPath = movie.BackdropPath,
            Genres = movie.Genres,
            OriginalLanguage = movie.OriginalLanguage,
            ImdbId = movie.ImdbId,
            VoteAverage = movie.VoteAverage,
            VoteCount = movie.VoteCount,
            Cast = MapCast(credits),
            Crew = MapCrew(credits),
            Videos = MapVideos(videos),
            Images = MapImages(images)
        };
    }

    private static List<MovieDetailsCastDto> MapCast(TmdbCreditsDto? credits) =>
        (credits?.Cast ?? [])
            .OrderBy(c => c.Order)
            .Take(MaxCastMembers)
            .Select(c => new MovieDetailsCastDto
            {
                Id = c.Id,
                Name = c.Name,
                OriginalName = c.OriginalName,
                Character = c.Character,
                ProfilePath = c.ProfilePath,
                Order = c.Order
            })
            .ToList();

    private static List<MovieDetailsCrewDto> MapCrew(TmdbCreditsDto? credits) =>
        (credits?.Crew ?? [])
            .Where(c => c.Job != null && RelevantCrewJobs.Contains(c.Job))
            .Select(c => new MovieDetailsCrewDto
            {
                Id = c.Id,
                Name = c.Name,
                OriginalName = c.OriginalName,
                Department = c.Department,
                Job = c.Job,
                ProfilePath = c.ProfilePath
            })
            .ToList();

    private static List<TmdbVideoDto> MapVideos(TmdbVideosDto? videos) =>
        (videos?.Results ?? [])
            .Where(v => v.Site == "YouTube" && v.Type != null && TrailerVideoTypes.Contains(v.Type))
            .OrderByDescending(v => v.Official)
            .ThenBy(v => string.Equals(v.Type, "Trailer", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

    private static List<MovieDetailsImageDto> MapImages(TmdbImagesDto? images)
    {
        if (images == null)
        {
            return [];
        }

        return images.Backdrops
            .Concat(images.Posters)
            .Take(MaxImages)
            .Select(i => new MovieDetailsImageDto
            {
                FilePath = i.FilePath,
                Width = i.Width,
                Height = i.Height,
                AspectRatio = i.AspectRatio
            })
            .ToList();
    }

    private async Task<TmdbCreditsDto?> TryGetCredits(int tmdbId)
    {
        try
        {
            return await GetCredits(tmdbId);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<TmdbVideosDto?> TryGetVideos(int tmdbId)
    {
        try
        {
            return await GetVideos(tmdbId);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<TmdbImagesDto?> TryGetImages(int tmdbId)
    {
        try
        {
            return await GetImages(tmdbId);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<T?> GetFromTmdb<T>(string path, bool notFoundReturnsNull = false)
    {
        var token = _configuration["TMDB_READ_ACCESS_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TMDB_READ_ACCESS_TOKEN is missing.");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, path);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);

        if (notFoundReturnsNull && response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"TMDB error: {(int)response.StatusCode} {response.StatusCode}. Response: {error}"
            );
        }

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
