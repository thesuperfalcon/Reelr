namespace backend.Features.Reviews
{
    // A plain-text reply to a review. Comments are flat, oldest first.
    public class ReviewComment
    {
        public const int MaxLength = 1000;

        public int Id { get; set; }

        public int ReviewId { get; set; }

        public Review Review { get; set; } = null!;

        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public string Text { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
