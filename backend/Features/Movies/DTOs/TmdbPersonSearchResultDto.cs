namespace backend.Features.Movies.DTOs;

public class TmdbPersonSearchResultDto
{
    public int Page { get; set; }

    public List<TmdbPersonDto> Results { get; set; } = [];
}
