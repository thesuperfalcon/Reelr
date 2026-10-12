using backend.Features.Users;

namespace backend.Features.Settings.DTOs
{
    public class UserSettingsDto
    {
        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public WatchlistVisibility WatchlistVisibility { get; set; }

        public DiaryVisibility DiaryVisibility { get; set; }

        public bool ShowFriendReviews { get; set; }

        public bool ShowOwnActivity { get; set; }

        // Whether the user may open the moderation page.
        public bool IsAdmin { get; set; }

        // A shared demo account: the account itself cannot be changed or deleted (see DemoAccounts).
        public bool IsDemo { get; set; }
    }

    // Returned after a change. Token is set when the username changed, since a rename revokes the old token.
    public class UpdatedSettingsDto : UserSettingsDto
    {
        public string? Token { get; set; }
    }

    // A new token for this device, after a change that revoked the old one.
    public class TokenDto
    {
        public string Token { get; set; } = string.Empty;
    }
}
