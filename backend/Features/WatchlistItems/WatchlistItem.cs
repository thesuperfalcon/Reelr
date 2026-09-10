namespace backend.Features.WatchlistItems
{
    public class WatchlistItem
    {
        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public DateTime AddedAt { get; set; }
    }
}
