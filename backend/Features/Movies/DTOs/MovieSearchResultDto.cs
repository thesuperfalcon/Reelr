namespace backend.Features.Movies.DTOs;

public class MovieSearchResultDto
{
    public int Page { get; set; }

    public List<TmdbSearchMovieDto> Movies { get; set; } = [];

    public List<TmdbPersonDto> Cast { get; set; } = [];

    public List<TmdbPersonDto> Crew { get; set; } = [];

    public List<TmdbCompanyDto> Studios { get; set; } = [];
}
