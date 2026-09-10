namespace backend.Features.Movies.DTOs;

public class MovieDetailsDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public string? Tagline { get; set; }

    public string? ReleaseDate { get; set; }

    public int? Runtime { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public List<TmdbGenreDto> Genres { get; set; } = [];

    public string? OriginalLanguage { get; set; }

    public string? ImdbId { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public List<MovieDetailsCastDto> Cast { get; set; } = [];

    public List<MovieDetailsCrewDto> Crew { get; set; } = [];

    public List<TmdbVideoDto> Videos { get; set; } = [];

    public List<MovieDetailsImageDto> Images { get; set; } = [];
}
