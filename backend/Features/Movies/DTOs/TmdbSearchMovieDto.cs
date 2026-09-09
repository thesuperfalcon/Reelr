using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs
{
    public class TmdbSearchMovieDto
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        public string? Overview { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }
    }

}
