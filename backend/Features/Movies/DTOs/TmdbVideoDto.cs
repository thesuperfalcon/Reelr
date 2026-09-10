namespace backend.Features.Movies.DTOs
{
    public class TmdbVideosDto
    {
        public List<TmdbVideoDto> Results { get; set; } = [];
    }

    public class TmdbVideoDto
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Site { get; set; }

        public string? Type { get; set; }

        public bool Official { get; set; }
    }
}
