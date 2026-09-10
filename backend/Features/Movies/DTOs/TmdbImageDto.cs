using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs
{
    public class TmdbImagesDto
    {
        public List<TmdbImageDto> Backdrops { get; set; } = [];

        public List<TmdbImageDto> Posters { get; set; } = [];
    }

    public class TmdbImageDto
    {
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        [JsonPropertyName("aspect_ratio")]
        public double AspectRatio { get; set; }
    }
}
