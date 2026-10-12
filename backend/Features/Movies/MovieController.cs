using System.ComponentModel.DataAnnotations;
using backend.Features.Auth;
using backend.Features.Movies.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Features.Movies;

[ApiController]
[Route("api/movies")]
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
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [EndpointSummary("Search and filter movies")]
    public async Task<ActionResult<TmdbSearchResultDto>> SearchMovies(
        [FromQuery, MaxLength(TmdbService.MaxQueryLength)] string? query,
        [FromQuery] int? castId,
        [FromQuery] int? crewId,
        [FromQuery] int? studioId,
        [FromQuery] int? genreId,
        [FromQuery, Range(1, TmdbService.MaxPage)] int page = 1)
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
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [EndpointSummary("Search movies, cast, crew and studios")]
    public async Task<ActionResult<MovieSearchResultDto>> SearchAll(
        [FromQuery, MaxLength(TmdbService.MaxQueryLength)] string query)
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

        // A TMDB failure becomes a 502 or 503 in TmdbExceptionHandler.
        var details = await _tmdbService.GetMovieDetails(tmdbId);

        if (details == null)
        {
            return NotFound();
        }

        return Ok(details);
    }
}
