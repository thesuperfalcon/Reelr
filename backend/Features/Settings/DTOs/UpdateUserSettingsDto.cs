using backend.Features.Users;
using System.ComponentModel.DataAnnotations;

namespace backend.Features.Settings.DTOs
{
    // Only the fields that are set change, so one setting can be saved on its own.
    public class UpdateUserSettingsDto
    {
        [MinLength(3)]
        public string? UserName { get; set; }

        public WatchlistVisibility? WatchlistVisibility { get; set; }

        public bool? ShowFriendReviews { get; set; }

        public bool? ShowOwnActivity { get; set; }
    }
}
