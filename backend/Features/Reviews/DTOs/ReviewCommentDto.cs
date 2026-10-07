namespace backend.Features.Reviews.DTOs
{
    public class ReviewCommentDto
    {
        public int Id { get; set; }

        public int ReviewId { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = null!;

        public string? ProfileImageUrl { get; set; }

        public string Text { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
