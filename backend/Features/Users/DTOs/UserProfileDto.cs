namespace backend.Features.Users.DTOs
{
    public class UserProfileDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public int FollowerCount { get; set; }

        public int FollowingCount { get; set; }

        // True when the logged-in caller follows this user. False for anonymous callers.
        public bool IsFollowing { get; set; }

        public WatchlistVisibility WatchlistVisibility { get; set; }

        // Whether the caller may open this user's watchlist.
        public bool CanSeeWatchlist { get; set; }

        public DiaryVisibility DiaryVisibility { get; set; }

        // Whether the caller may open this user's diary.
        public bool CanSeeDiary { get; set; }

        // True when the logged-in caller has blocked this user.
        public bool IsBlocked { get; set; }
    }
}
