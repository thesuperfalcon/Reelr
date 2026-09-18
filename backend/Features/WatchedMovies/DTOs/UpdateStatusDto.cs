namespace backend.Features.WatchedMovies.DTOs
{
    public class UpdateStatusDto
    {
        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
