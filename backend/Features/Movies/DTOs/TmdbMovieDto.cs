using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs;

public class TmdbMovieDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Overview { get; set; }

    public string? Tagline { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? BackdropPath { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    public int? Runtime { get; set; }

    public List<TmdbGenreDto> Genres { get; set; } = [];

    [JsonPropertyName("vote_average")]
    public decimal VoteAverage { get; set; }

    [JsonPropertyName("vote_count")]
    public int VoteCount { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("original_language")]
    public string? OriginalLanguage { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }
}
