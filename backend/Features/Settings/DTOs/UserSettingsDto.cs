using backend.Features.Users;

namespace backend.Features.Settings.DTOs
{
    public class UserSettingsDto
    {
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public WatchlistVisibility WatchlistVisibility { get; set; }

        public bool ShowFriendReviews { get; set; }

        public bool ShowOwnActivity { get; set; }
    }

    // Returned after a change. Token is set when the username changed, since the old token still carries the old name.
    public class UpdatedSettingsDto : UserSettingsDto
    {
        public string? Token { get; set; }
    }
}
