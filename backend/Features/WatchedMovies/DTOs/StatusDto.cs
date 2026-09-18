namespace backend.Features.WatchedMovies.DTOs
{
    public class StatusDto
    {
        public int TmdbId { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }

        public DateTime WatchedAt { get; set; }
    }
}
