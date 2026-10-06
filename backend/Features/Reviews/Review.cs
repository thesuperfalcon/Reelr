namespace backend.Features.Reviews
{
    public class Review
    {
        // Longest review text in characters. Markdown syntax counts toward it.
        public const int MaxLength = 5000;

        public int Id { get; set; }

        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public string Text { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // The diary entry that logged this review. Deleting that entry deletes the review.
        public int? DiaryEntryId { get; set; }

        public Diary.DiaryEntry? DiaryEntry { get; set; }
    }
}
