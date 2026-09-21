namespace backend.Features.Users.DTOs
{
    public class UserProfileDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }

        public int FollowerCount { get; set; }

        public int FollowingCount { get; set; }
    }
}
