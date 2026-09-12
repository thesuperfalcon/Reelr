using backend.Features.Movies.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.Movies;

[ApiController]
[Route("api/[controller]")]
public class MovieController : ControllerBase
{
    private readonly TmdbService _tmdbService;

    public MovieController(TmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    [HttpGet("{tmdbId:int}")]
    [EndpointSummary("Get movie")]
    public async Task<ActionResult<TmdbMovieDto>> GetMovie(int tmdbId)
    {
        var movie = await _tmdbService.GetMovie(tmdbId);

        if (movie == null)
        {
            return NotFound();
        }

        return Ok(movie);
    }

    [HttpGet("search")]
    [EndpointSummary("Search and filter movies")]
    public async Task<ActionResult<TmdbSearchResultDto>> SearchMovies(
        [FromQuery] string? query,
        [FromQuery] int? castId,
        [FromQuery] int? crewId,
        [FromQuery] int? studioId,
        [FromQuery] int? genreId,
        [FromQuery] int page = 1)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            var titleResults = await _tmdbService.SearchMovies(query, page);

            return Ok(titleResults);
        }

        if (castId == null && crewId == null && studioId == null && genreId == null)
        {
            return BadRequest("Provide query, castId, crewId, studioId or genreId.");
        }

        var filteredResults = await _tmdbService.DiscoverMovies(castId, crewId, studioId, genreId, page);

        if (filteredResults == null)
        {
            return NotFound();
        }

        return Ok(filteredResults);
    }

    [HttpGet("search/all")]
    [EndpointSummary("Search movies, cast, crew and studios")]
    public async Task<ActionResult<MovieSearchResultDto>> SearchAll([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query must not be empty.");
        }

        return Ok(await _tmdbService.SearchAll(query));
    }

    [HttpGet("trending")]
    [EndpointSummary("Get trending movies")]
    public async Task<ActionResult<TmdbSearchResultDto>> GetTrendingMovies()
    {
        var movies = await _tmdbService.GetTrendingMovies();

        if (movies == null)
        {
            return NotFound();
        }

        return Ok(movies);
    }

    [HttpGet("popular")]
    [EndpointSummary("Get popular movies")]
    public async Task<ActionResult<TmdbSearchResultDto>> GetPopularMovies()
    {
        var movies = await _tmdbService.GetPopularMovies();

        if (movies == null)
        {
            return NotFound();
        }

        return Ok(movies);
    }

    [HttpGet("{tmdbId:int}/recommended")]
    [EndpointSummary("Get recommended movies")]
    public async Task<ActionResult<TmdbSearchResultDto>> GetRecommendedMovies(int tmdbId)
    {
        if (tmdbId <= 0)
        {
            return BadRequest("tmdbId must be a positive integer.");
        }

        var movies = await _tmdbService.GetRecommendedMovies(tmdbId);

        if (movies == null)
        {
            return NotFound();
        }

        return Ok(movies);
    }

    [HttpGet("{tmdbId:int}/similar")]
    [EndpointSummary("Get similar movies")]
    [EndpointDescription("Based only on genres and plot keywords, so results are not always accurate.")]
    public async Task<ActionResult<TmdbSearchResultDto>> GetSimilarMovies(int tmdbId)
    {
        if (tmdbId <= 0)
        {
            return BadRequest("tmdbId must be a positive integer.");
        }

        var movies = await _tmdbService.GetSimilarMovies(tmdbId);

        if (movies == null)
        {
            return NotFound();
        }

        return Ok(movies);
    }

    [HttpGet("{tmdbId:int}/credits")]
    [EndpointSummary("Get cast and crew")]
    public async Task<ActionResult<TmdbCreditsDto>> GetCredits(int tmdbId)
    {
        var credits = await _tmdbService.GetCredits(tmdbId);

        if (credits == null)
        {
            return NotFound();
        }

        return Ok(credits);
    }

    [HttpGet("{tmdbId:int}/details")]
    [EndpointSummary("Get movie details")]
    public async Task<ActionResult<MovieDetailsDto>> GetMovieDetails(int tmdbId)
    {
        if (tmdbId <= 0)
        {
            return BadRequest("tmdbId must be a positive integer.");
        }

        MovieDetailsDto? details;

        try
        {
            details = await _tmdbService.GetMovieDetails(tmdbId);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                $"Could not fetch data from TMDB: {ex.Message}"
            );
        }

        if (details == null)
        {
            return NotFound();
        }

        return Ok(details);
    }
}
