using Microsoft.AspNetCore.Identity;

namespace backend.Features.Users
{
    public class User : IdentityUser<int>
    {
        public string? ProfileImageUrl { get; set; }

        public WatchlistVisibility WatchlistVisibility { get; set; } = WatchlistVisibility.Followers;

        // Whether the start page shows "Reviews from friends".
        public bool ShowFriendReviews { get; set; } = true;

        public ICollection<WatchedMovies.WatchedMovie> WatchedMovies { get; set; } = [];

        public ICollection<Ratings.Rating> Ratings { get; set; } = [];

        public ICollection<Reviews.Review> Reviews { get; set; } = [];

        public ICollection<WatchlistItems.WatchlistItem> Watchlist { get; set; } = [];
    }
}
