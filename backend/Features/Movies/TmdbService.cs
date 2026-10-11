using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.Features.Movies.DTOs;
using backend.Features.People.DTOs;
using Polly;

namespace backend.Features.Movies;

public class TmdbService
{
    // Limits for search input, checked by the controllers. TMDB itself serves at most 500 pages.
    public const int MaxQueryLength = 100;
    public const int MaxPage = 500;

    private const int MaxCastMembers = 10;
    private const int MaxImages = 20;

    // How long answers are cached: lists change daily, film and person data hardly ever.
    private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DetailsCacheDuration = TimeSpan.FromHours(6);

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
    private readonly TmdbCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TmdbService> _logger;

    public TmdbService(HttpClient httpClient, TmdbCache cache, IConfiguration configuration, ILogger<TmdbService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<TmdbMovieDto?> GetMovie(int tmdbId) =>
        GetFromTmdb<TmdbMovieDto>($"movie/{tmdbId}?language=en-US", DetailsCacheDuration, notFoundReturnsNull: true);

    public Task<TmdbSearchResultDto?> SearchMovies(string query, int page = 1) =>
        GetFromTmdb<TmdbSearchResultDto>(
            $"search/movie?query={Uri.EscapeDataString(query)}&language=en-US&page={page}", SearchCacheDuration);

    public Task<TmdbPersonSearchResultDto?> SearchPeople(string query, int page = 1) =>
        GetFromTmdb<TmdbPersonSearchResultDto>(
            $"search/person?query={Uri.EscapeDataString(query)}&language=en-US&page={page}", SearchCacheDuration);

    public Task<TmdbPersonDetailsDto?> GetPerson(int personId) =>
        GetFromTmdb<TmdbPersonDetailsDto>(
            $"person/{personId}?append_to_response=movie_credits,external_ids&language=en-US",
            DetailsCacheDuration,
            notFoundReturnsNull: true);

    public Task<TmdbCompanySearchResultDto?> SearchCompanies(string query, int page = 1) =>
        GetFromTmdb<TmdbCompanySearchResultDto>(
            $"search/company?query={Uri.EscapeDataString(query)}&page={page}", SearchCacheDuration);

    public async Task<MovieSearchResultDto> SearchAll(string query, int page = 1)
    {
        var moviesTask = SearchMovies(query, page);
        var peopleTask = SearchPeople(query, page);
        var companiesTask = SearchCompanies(query, page);

        await Task.WhenAll(moviesTask, peopleTask, companiesTask);

        var movies = await moviesTask;
        var people = await peopleTask;
        var companies = await companiesTask;

        var peopleResults = people?.Results ?? [];

        var cast = peopleResults.Where(p => p.KnownForDepartment == "Acting")
            .OrderByDescending(p => p.Popularity)
            .ToList();

        var crew = peopleResults.Where(p => p.KnownForDepartment != "Acting")
            .OrderByDescending(p => p.Popularity)
            .ToList();

        return new MovieSearchResultDto
        {
            Page = page,
            Movies = BoostExactMatch(movies?.Results ?? [], query, m => m.Title),
            Cast = BoostExactMatch(cast, query, p => p.Name),
            Crew = BoostExactMatch(crew, query, p => p.Name),
            Studios = BoostExactMatch(companies?.Results ?? [], query, c => c.Name)
        };
    }

    // TMDB/Letterboxd-style boosting: an exact title/name match ranks above popularity.
    private static List<T> BoostExactMatch<T>(List<T> items, string query, Func<T, string?> nameSelector)
    {
        var exactIndex = items.FindIndex(i => string.Equals(nameSelector(i), query, StringComparison.OrdinalIgnoreCase));

        if (exactIndex <= 0)
        {
            return items;
        }

        var exactMatch = items[exactIndex];
        items.RemoveAt(exactIndex);
        items.Insert(0, exactMatch);

        return items;
    }

    public Task<TmdbSearchResultDto?> DiscoverMovies(int? castId, int? crewId, int? studioId, int? genreId, int page = 1)
    {
        var queryParams = new List<string> { "language=en-US", $"page={page}" };

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

        return GetFromTmdb<TmdbSearchResultDto>($"discover/movie?{string.Join("&", queryParams)}", SearchCacheDuration);
    }

    public Task<TmdbSearchResultDto?> GetRecommendedMovies(int tmdbId) =>
        GetFromTmdb<TmdbSearchResultDto>(
            $"movie/{tmdbId}/recommendations?language=en-US", DetailsCacheDuration, notFoundReturnsNull: true);

    // TMDB note: this method only looks for other items based on genres and plot keywords,
    // so results are not always going to be accurate. Use it with that in mind.
    public Task<TmdbSearchResultDto?> GetSimilarMovies(int tmdbId) =>
        GetFromTmdb<TmdbSearchResultDto>(
            $"movie/{tmdbId}/similar?language=en-US", DetailsCacheDuration, notFoundReturnsNull: true);

    public Task<TmdbSearchResultDto?> GetTrendingMovies() =>
        GetFromTmdb<TmdbSearchResultDto>("trending/movie/week?language=en-US", ListCacheDuration);

    public Task<TmdbSearchResultDto?> GetPopularMovies() =>
        GetFromTmdb<TmdbSearchResultDto>("movie/popular?language=en-US", ListCacheDuration);

    public Task<TmdbCreditsDto?> GetCredits(int tmdbId) =>
        GetFromTmdb<TmdbCreditsDto>($"movie/{tmdbId}/credits?language=en-US", DetailsCacheDuration, notFoundReturnsNull: true);

    public Task<TmdbVideosDto?> GetVideos(int tmdbId) =>
        GetFromTmdb<TmdbVideosDto>($"movie/{tmdbId}/videos?language=en-US", DetailsCacheDuration);

    public Task<TmdbImagesDto?> GetImages(int tmdbId) =>
        GetFromTmdb<TmdbImagesDto>($"movie/{tmdbId}/images", DetailsCacheDuration);

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
        catch (TmdbUnavailableException)
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
        catch (TmdbUnavailableException)
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
        catch (TmdbUnavailableException)
        {
            return null;
        }
    }

    // Every TMDB call goes through here. A successful answer is cached for cacheFor; a 404 is not cached.
    // Any way TMDB can fail becomes a TmdbUnavailableException, so callers and the API handle one exception type.
    private async Task<T?> GetFromTmdb<T>(string path, TimeSpan cacheFor, bool notFoundReturnsNull = false) where T : class
    {
        if (_cache.TryGet(path, out T? cached))
        {
            return cached;
        }

        var token = _configuration["TMDB_READ_ACCESS_TOKEN"];

        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException("TMDB_READ_ACCESS_TOKEN is missing.");
        }

        string json;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);

            if (notFoundReturnsNull && response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                // The response body only goes to the log; clients get a general problem body.
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("TMDB request {Path} failed with {StatusCode}: {Body}", LogPath(path), (int)response.StatusCode, error);

                throw new TmdbUnavailableException(
                    $"TMDB returned {(int)response.StatusCode}.", StatusCodes.Status502BadGateway);
            }

            json = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "TMDB request {Path} could not be sent", LogPath(path));
            throw new TmdbUnavailableException("Could not reach TMDB.", StatusCodes.Status502BadGateway, ex);
        }
        catch (Exception ex) when (ex is TaskCanceledException or ExecutionRejectedException)
        {
            // A timeout, or the circuit breaker skipping TMDB for a while after many failures.
            _logger.LogWarning(ex, "TMDB request {Path} timed out or was skipped", LogPath(path));
            throw new TmdbUnavailableException("TMDB did not answer in time.", StatusCodes.Status503ServiceUnavailable, ex);
        }

        T? result;

        try
        {
            result = JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "TMDB request {Path} returned JSON that could not be read", LogPath(path));
            throw new TmdbUnavailableException("TMDB sent an answer that could not be read.", StatusCodes.Status502BadGateway, ex);
        }

        if (result != null)
        {
            _cache.Set(path, result, json.Length, cacheFor);
        }

        return result;
    }

    // The path without its query string. Searches carry what the user typed, which does not belong in the log.
    private static string LogPath(string path)
    {
        var query = path.IndexOf('?');
        return query < 0 ? path : path[..query];
    }
}
