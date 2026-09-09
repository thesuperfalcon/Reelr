using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs
{
    public class TmdbCreditsDto
    {
        public List<TmdbCastDto> Cast { get; set; } = [];

        public List<TmdbCrewDto> Crew { get; set; } = [];
    }

    public class TmdbCastDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("original_name")]
        public string? OriginalName { get; set; }

        public string? Character { get; set; }

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }

        public int Order { get; set; }
    }

    public class TmdbCrewDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("original_name")]
        public string? OriginalName { get; set; }

        public string? Department { get; set; }

        public string? Job { get; set; }

        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }
    }
}
