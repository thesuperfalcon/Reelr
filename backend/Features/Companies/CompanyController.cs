using backend.Features.Movies;
using backend.Features.Movies.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.Companies;

[ApiController]
[Route("api/[controller]")]
public class CompanyController : ControllerBase
{
    private readonly TmdbService _tmdbService;

    public CompanyController(TmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    [HttpGet("search")]
    [EndpointSummary("Search companies")]
    public async Task<ActionResult<TmdbCompanySearchResultDto>> SearchCompanies([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query must not be empty.");
        }

        var companies = await _tmdbService.SearchCompanies(query);

        if (companies == null)
        {
            return NotFound();
        }

        return Ok(companies);
    }
}
