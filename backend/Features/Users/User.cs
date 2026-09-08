using backend.Features;


namespace backend.Features.Users
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string? ProfileImageUrl { get; set; }

        public ICollection<WatchedMovies.WatchedMovie> WatchedMovies { get; set; } = [];

        public ICollection<Ratings.Rating> Ratings { get; set; } = [];

        public ICollection<Reviews.Review> Reviews { get; set; } = [];

        public ICollection<WatchlistItems.WatchlistItem> Watchlist { get; set; } = [];
    }
}
