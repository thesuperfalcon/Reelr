using backend.Features;

namespace backend.Features.WatchedMovies
{
    public class WatchedMovie
    {
        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public DateTime WatchedAt { get; set; }
    }
}
