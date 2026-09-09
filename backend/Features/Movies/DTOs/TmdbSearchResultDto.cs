using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs;

public class TmdbSearchResultDto
{
    public int Page { get; set; }

    public List<TmdbSearchMovieDto> Results { get; set; } = [];
}

