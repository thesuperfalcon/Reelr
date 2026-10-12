namespace backend.Features.Users
{
    // One user blocking another. The blocked user cannot follow, comment on or like the blocker's content,
    // and the blocker no longer sees the blocked user's reviews, comments or community activity.
    public class Block
    {
        public int BlockerId { get; set; }

        public User Blocker { get; set; } = null!;

        public int BlockedId { get; set; }

        public User Blocked { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
