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
    public async Task<ActionResult<TmdbSearchResultDto>> SearchMovies(
    [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query får inte vara tom.");
        }

        var movies = await _tmdbService.SearchMovies(query);

        return Ok(movies);
    }

    [HttpGet("{tmdbId:int}/credits")]
    public async Task<ActionResult<TmdbCreditsDto>> GetCredits(int tmdbId)
    {
        var credits = await _tmdbService.GetCredits(tmdbId);

        if (credits == null)
        {
            return NotFound();
        }

        return Ok(credits);
    }
}
