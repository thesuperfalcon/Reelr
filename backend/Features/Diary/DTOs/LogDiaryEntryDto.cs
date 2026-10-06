using System.ComponentModel.DataAnnotations;

namespace backend.Features.Diary.DTOs
{
    public class LogDiaryEntryDto
    {
        // Null leaves the current rating as it is.
        [Range(typeof(decimal), "0", "5")]
        public decimal? Score { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }

        // The day the film was watched. Null means now.
        public DateOnly? WatchedOn { get; set; }

        // Markdown text that creates or replaces the user's review. Null or blank leaves the review as it is.
        [MaxLength(Reviews.Review.MaxLength)]
        public string? Review { get; set; }
    }
}
