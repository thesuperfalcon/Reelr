namespace backend.Features.Reviews.DTOs
{
    public class ReviewDto
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = null!;

        public string? ProfileImageUrl { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }

        public string Text { get; set; } = null!;

        public bool ContainsSpoilers { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        // Whether the caller likes this review. Always false without a token.
        public bool LikedByMe { get; set; }

        public decimal? Score { get; set; }

        // Watch date of the diary entry that logged the review. Null for reviews without one.
        public DateTime? WatchedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
