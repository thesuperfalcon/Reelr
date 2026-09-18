namespace backend.Features.WatchedMovies.DTOs
{
    public class CreateStatusDto
    {
        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
