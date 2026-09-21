namespace backend.Features.MovieLists.DTOs
{
    public class MovieListItemDto
    {
        public int TmdbId { get; set; }
        public string Title { get; set; } = null!;
        public string? PosterUrl { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
