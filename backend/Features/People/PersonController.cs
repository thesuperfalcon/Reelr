using System.ComponentModel.DataAnnotations;
using backend.Features.Movies;
using backend.Features.Movies.DTOs;
using backend.Features.People.DTOs;
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

    [HttpGet("{personId:int}")]
    [EndpointSummary("Get a person's details and film credits")]
    public async Task<ActionResult<PersonDetailsDto>> GetPerson(int personId)
    {
        if (personId <= 0)
        {
            return BadRequest("personId must be a positive integer.");
        }

        // A TMDB failure becomes a 502 or 503 in TmdbExceptionHandler.
        var person = await _tmdbService.GetPerson(personId);

        if (person == null)
        {
            return NotFound();
        }

        return Ok(PersonCredits.ToDetails(person));
    }

    [HttpGet("search")]
    [EndpointSummary("Search people")]
    public async Task<ActionResult<TmdbPersonSearchResultDto>> SearchPeople(
        [FromQuery, MaxLength(TmdbService.MaxQueryLength)] string query)
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
