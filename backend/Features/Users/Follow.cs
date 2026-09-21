namespace backend.Features.Users
{
    public class Follow
    {
        public int FollowerId { get; set; }

        public User Follower { get; set; } = null!;

        public int FollowedId { get; set; }

        public User Followed { get; set; } = null!;
    }
}
