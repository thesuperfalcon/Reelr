namespace backend.Features.WatchlistItems.DTOs
{
    public class WatchlistEntryDto
    {
        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }

        public DateTime AddedAt { get; set; }
    }
}
