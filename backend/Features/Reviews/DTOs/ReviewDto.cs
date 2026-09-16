namespace backend.Features.Reviews.DTOs
{
    public class ReviewDto
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = null!;

        public int TmdbId { get; set; }

        public string Text { get; set; } = null!;

        public decimal? Score { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
