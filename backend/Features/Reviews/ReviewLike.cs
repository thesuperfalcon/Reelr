namespace backend.Features.Reviews
{
    // One user liking someone else's review. A user likes a review at most once.
    public class ReviewLike
    {
        public int ReviewId { get; set; }

        public Review Review { get; set; } = null!;

        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
