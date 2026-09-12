namespace backend.Features.Movies.DTOs;

public class TmdbCompanySearchResultDto
{
    public int Page { get; set; }

    public List<TmdbCompanyDto> Results { get; set; } = [];
}
