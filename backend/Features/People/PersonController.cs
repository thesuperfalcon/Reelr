using backend.Features.Movies;
using backend.Features.Movies.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.People;

[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly TmdbService _tmdbService;

    public PersonController(TmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    [HttpGet("search")]
    [EndpointSummary("Search people")]
    public async Task<ActionResult<TmdbPersonSearchResultDto>> SearchPeople([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query must not be empty.");
        }

        var people = await _tmdbService.SearchPeople(query);

        if (people == null)
        {
            return NotFound();
        }

        return Ok(people);
    }
}
