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
}
